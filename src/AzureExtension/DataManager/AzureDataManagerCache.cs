// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Dynamic;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Account;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;

namespace DevHomeAzureExtension;

public partial class AzureDataManager
{
    private static readonly Uri _vSAccountUri = new(@"https://app.vssps.visualstudio.com/");

    private static readonly int _pullRequestProjectLimit = 50;

    private static readonly int _pullRequestRepositoryLimit = 25;

    private static readonly TimeSpan _orgUpdateDelayTime = TimeSpan.FromSeconds(5);

    public async Task UpdateDataForAccountsAsync(RequestOptions? options = null, Guid? requestor = null)
    {
        // A parameterless call will always update the data, effectively a 'force update'.
        await UpdateDataForAccountsAsync(TimeSpan.MinValue, options, requestor);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task UpdateDataForAccountsAsync(TimeSpan olderThan, RequestOptions? options = null, Guid? requestor = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var cancellationToken = options?.CancellationToken.GetValueOrDefault() ?? default;
        var requestorGuid = requestor ?? Guid.Empty;
        var errors = 0;
        var accountsUpdated = 0;
        var accountsSkipped = 0;
        var startTime = DateTime.UtcNow;
        Exception? firstException = null;

        // Refresh option will clear all sync data, effectively forcing every row to be updated.
        if (options is not null && options.Refresh)
        {
            using var tx = DataStore.Connection!.BeginTransaction();
            try
            {
                _log.Debug("Clearing sync data.");
                Organization.ClearAllSyncData(DataStore);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed clearing sync data.");
                tx.Rollback();
            }

            tx.Commit();
        }

        IEnumerable<DeveloperId.DeveloperId>? developerIds;
        try
        {
            developerIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIdsInternal();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed getting logged in developer ids.");
            firstException = ex;
            ++errors;
            developerIds = [];
        }

        foreach (var developerId in developerIds)
        {
            _log.Debug($"Updating accounts for {developerId.LoginId}, older than {olderThan}");
            var accounts = GetAccounts(developerId, cancellationToken);
            foreach (var account in accounts)
            {
                var org = Organization.Get(DataStore, account.AccountUri.ToString());
                if (org is not null && ((DateTime.UtcNow - org.LastSyncAt) < olderThan))
                {
                    _log.Debug($"Organization: {org.Name} has recently been updated, skipping.");
                    ++accountsSkipped;
                    continue;
                }

                // Transactional unit is the account (organization), which chains down to Project
                // and repository.
                using var tx = DataStore.Connection!.BeginTransaction();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(account.AccountUri, developerId);

                    // If connection is null the organization is disabled
                    if (connection is null)
                    {
                        ++accountsSkipped;
                        _log.Information($"Account {account.AccountName} had a null connection, treating as disabled.");
                        tx.Rollback();
                        continue;
                    }

                    var orgStartTime = DateTime.UtcNow;

                    UpdateOrganization(account, developerId, connection, cancellationToken);

                    tx.Commit();
                    _log.Information($"Updated organization: {account.AccountName}");

                    // Send an update event once the transaction is completed so anyone waiting on the DB to be
                    // available has an opportunity to use it.
                    var orgUpdateContext = CreateUpdateEventContext(0, 1, 0, DateTime.UtcNow - orgStartTime);
                    SendAccountUpdateEvent(_log, this, requestorGuid, orgUpdateContext, firstException);
                    ++accountsUpdated;

                    // Delay to allow widgets and other code to respond to the event and use the database.
                    // This is to prevent DOS'ing widgets using the datastore during large cache updates of
                    // many organizations.
                    await Task.Delay(_orgUpdateDelayTime, cancellationToken);
                }
                catch (Exception ex) when (IsCancelException(ex))
                {
                    firstException ??= ex;
                    tx.Rollback();
                    _log.Information("Operation was cancelled.");
                    var cancelContext = CreateUpdateEventContext(errors, accountsUpdated, accountsSkipped, DateTime.UtcNow - startTime);
                    SendCancelUpdateEvent(_log, this, requestorGuid, cancelContext, firstException);
                    return;
                }
                catch (Exception ex)
                {
                    firstException ??= ex;
                    tx.Rollback();
                    _log.Error(ex, $"Unexpected failure updating account: {account.AccountName}");
                    ++errors;
                }
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        _log.Information($"Updating all account data complete. Elapsed: {elapsed.TotalSeconds}s");
        var context = CreateUpdateEventContext(errors, accountsUpdated, accountsSkipped, elapsed);
        SendCacheUpdateEvent(_log, this, requestorGuid, context, firstException);
    }

    // Returns a dynamic object with event context for reporting.
    private dynamic CreateUpdateEventContext(int errors, int accountsUpdated, int accountsSkipped, TimeSpan elapsed)
    {
        dynamic context = new ExpandoObject();
        var contextDict = (IDictionary<string, object>)context;
        contextDict.Add("AccountsUpdated", accountsUpdated);
        contextDict.Add("AccountsSkipped", accountsSkipped);
        contextDict.Add("Errors", errors);
        contextDict.Add("TimeElapsed", elapsed);
        return context;
    }

    private void UpdateOrganization(Account account, DeveloperId.DeveloperId developerId, VssConnection connection, CancellationToken cancellationToken)
    {
        // Update account identity information:
        var identity = Identity.GetOrCreateIdentity(DataStore, connection.AuthorizedIdentity, connection, developerId.LoginId);

        _log.Verbose($"Updating organization: {account.AccountName}");
        var organization = Organization.GetOrCreate(DataStore, account.AccountUri);
        var projects = GetProjects(account, connection);
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _log.Verbose($"Updating project: {project.Name}");
            var dsProject = Project.GetOrCreateByTeamProject(DataStore, project, organization.Id);

            // Get the project's pull request count for this developer, add it.
            var projectPullRequestCount = GetPullRequestsForProject(project, connection, developerId, cancellationToken).Count;
            if (projectPullRequestCount > 0)
            {
                ProjectReference.GetOrCreate(DataStore, dsProject.Id, identity.Id, projectPullRequestCount);
            }

            var repositories = GetGitRepositories(project, connection, cancellationToken);
            foreach (var repository in repositories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log.Verbose($"Updating repository: {repository.Name}");
                var dsRepository = Repository.GetOrCreate(DataStore, repository, dsProject.Id);

                // If the project had pull requests, we need to check if this repository has any.
                // We don't waste rest calls on every repository unless we know the project had at least one.
                if (projectPullRequestCount > 0)
                {
                    var repositoryPullRequestCount = GetPullRequestsForRepository(repository, connection, developerId, cancellationToken).Count;
                    if (repositoryPullRequestCount > 0)
                    {
                        // If non-zero, add a repository reference.
                        RepositoryReference.GetOrCreate(DataStore, dsRepository.Id, identity.Id, repositoryPullRequestCount);
                    }
                }
            }
        }

        organization.SetSynced();
    }

    private List<Account> GetAccounts(DeveloperId.DeveloperId developerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(_vSAccountUri, developerId);
            var client = connection.GetClient<AccountHttpClient>();
            return client.GetAccountsByMemberAsync(memberId: connection.AuthorizedIdentity.Id, cancellationToken: cancellationToken).Result;
        }
        catch (Exception ex) when (!IsCancelException(ex))
        {
            _log.Error(ex, "Failed querying for organizations.");
            return [];
        }
    }

    private List<TeamProjectReference> GetProjects(Account account, VssConnection connection)
    {
        try
        {
            var client = connection.GetClient<ProjectHttpClient>();
            var projects = client.GetProjects().Result;
            return [.. projects];
        }
        catch (VssServiceException vssEx)
        {
            _log.Information($"Failed getting projects for account: {account.AccountName}. {vssEx.Message}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Failed getting projects for account: {account.AccountName}");
        }

        return [];
    }

    private List<GitRepository> GetGitRepositories(TeamProjectReference project, VssConnection connection, CancellationToken cancellationToken = default)
    {
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();
            var repositories = gitClient.GetRepositoriesAsync(project.Id, false, false, cancellationToken).Result;
            return [.. repositories];
        }
        catch (Exception ex) when (!IsCancelException(ex))
        {
            _log.Error(ex, $"Failed getting repositories for project: {project.Name}");
        }

        return [];
    }

    private List<GitPullRequest> GetPullRequestsForProject(TeamProjectReference project, VssConnection connection, DeveloperId.DeveloperId developerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();

            // We are interested in pull requests where the developer is the author.
            var searchCriteria = new GitPullRequestSearchCriteria
            {
                CreatorId = connection.AuthorizedIdentity.Id,
                IncludeLinks = false,
                Status = PullRequestStatus.All,
            };

            // We expect most results will be empty but for the sake of performance we will limit the number.
            var pullRequests = gitClient.GetPullRequestsByProjectAsync(project.Id, searchCriteria, null, null, _pullRequestProjectLimit, null, cancellationToken).Result;
            return [..pullRequests];
        }
        catch (VssServiceException vssEx)
        {
            _log.Debug($"Unable to access project pull requests: {project.Name}: {vssEx.Message}");
        }
        catch (Exception ex) when (!IsCancelException(ex))
        {
            _log.Error(ex, $"Failed getting pull requests for project: {project.Name}");
        }

        return [];
    }

    private List<GitPullRequest> GetPullRequestsForRepository(GitRepository repository, VssConnection connection, DeveloperId.DeveloperId developerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();

            // We are interested in pull requests where the developer is the author.
            var searchCriteria = new GitPullRequestSearchCriteria
            {
                CreatorId = connection.AuthorizedIdentity.Id,
                IncludeLinks = false,
                Status = PullRequestStatus.All,
            };

            // We expect most results will be empty but for the sake of performance we will limit the number.
            var pullRequests = gitClient.GetPullRequestsAsync(repository.Id, searchCriteria, null, null, _pullRequestRepositoryLimit, null, cancellationToken).Result;
            return [.. pullRequests];
        }
        catch (AggregateException aggEx) when (aggEx.InnerException is VssServiceException)
        {
            // There are likely many repositories to which the user does not have access.
            // This is expected, and if the user does not have access then it is a safe
            // assumption that they do not have any pull requests there, so we will not
            // treat this as an error.
            // Specific error is: TF401019
            _log.Debug($"Unable to access repository pull requests: {repository.Name}: {aggEx.InnerException?.Message}");
        }
        catch (Exception ex) when (!IsCancelException(ex))
        {
            _log.Error(ex, $"Failed getting pull requests for repository: {repository.Name}");
        }

        return [];
    }

    private static void SendCacheUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception? ex)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Cache, requestor, context, ex);
    }

    private static void SendAccountUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception? ex)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Account, requestor, context, ex);
    }

    private static bool IsCancelException(Exception ex)
    {
        return (ex is OperationCanceledException) || (ex is TaskCanceledException);
    }
}
