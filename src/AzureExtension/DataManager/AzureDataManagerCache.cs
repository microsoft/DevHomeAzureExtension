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
    private static readonly Uri VSAccountUri = new(@"https://app.vssps.visualstudio.com/");

    public async Task UpdateDataForAccountsAsync(RequestOptions? options = null, Guid? requestor = null)
    {
        // A parameterless call will always update the data, effectively a 'force update'.
        await UpdateDataForAccountsAsync(TimeSpan.MinValue, options, requestor);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task UpdateDataForAccountsAsync(TimeSpan olderThan, RequestOptions? options = null, Guid? requestor = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var cancellationToken = options?.CancellationToken.GetValueOrDefault();
        var requestorGuid = requestor ?? Guid.Empty;
        var errors = 0;
        var accountsUpdated = 0;
        var accountsSkipped = 0;
        var startTime = DateTime.Now;
        Exception? firstException = null;

        // Refresh option will clear all sync data, effectively forcing every row to be updated.
        if (options is not null && options.Refresh)
        {
            using var tx = DataStore.Connection!.BeginTransaction();
            try
            {
                _log.Debug("Clearing sync data.");
                Organization.ClearAllSynced(DataStore);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed clearing sync data.");
                tx.Rollback();
            }

            tx.Commit();
        }

        var developerIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIdsInternal();
        foreach (var developerId in developerIds)
        {
            _log.Debug($"Updating accounts for {developerId.LoginId}, older than {olderThan}");
            var accounts = GetAccounts(developerId);
            foreach (var account in accounts)
            {
                var org = Organization.Get(DataStore, account.AccountUri.ToString());
                if (org is not null)
                {
                    if ((DateTime.Now - org.LastSyncAt) < olderThan)
                    {
                        _log.Information($"Organization: {org.Name} has recently been updated, skipping.");
                        ++accountsSkipped;
                        continue;
                    }
                }

                // Transactional unit is the account (organization), which chains down to Project
                // and repository.
                using var tx = DataStore.Connection!.BeginTransaction();
                try
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(account.AccountUri, developerId);
                    _log.Verbose($"Updating organization: {account.AccountName}");
                    var organization = Organization.GetOrCreate(DataStore, account.AccountUri);
                    var projects = GetProjects(account, connection);
                    foreach (var project in projects)
                    {
                        cancellationToken?.ThrowIfCancellationRequested();
                        _log.Verbose($"Updating project: {project.Name}");
                        var dsProject = Project.GetOrCreateByTeamProject(DataStore, project, organization.Id);
                        var repositories = GetGitRepositories(project, connection);
                        foreach (var repository in repositories)
                        {
                            cancellationToken?.ThrowIfCancellationRequested();
                            _log.Verbose($"Updating repository: {repository.Name}");
                            var dsRepository = Repository.GetOrCreate(DataStore, repository, dsProject.Id);
                        }

                        // TODO: Fetch pull requests for the project and create references to any existing repositories.
                    }

                    organization.SetSynced();
                    tx.Commit();
                    _log.Information($"Updated organization: {account.AccountName}");
                    ++accountsUpdated;
                }
                catch (OperationCanceledException cancelException)
                {
                    firstException ??= cancelException;
                    tx.Rollback();
                    _log.Information("Operation was cancelled.");
                    dynamic cancelContext = new ExpandoObject();
                    var cancelContextDict = (IDictionary<string, object>)cancelContext;
                    cancelContextDict.Add("AccountsUpdated", accountsUpdated);
                    cancelContextDict.Add("AccountsSkipped", accountsSkipped);
                    cancelContextDict.Add("Errors", errors);
                    cancelContextDict.Add("TimeElapsed", DateTime.Now - startTime);
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

        var elapsed = DateTime.Now - startTime;
        _log.Information($"Updating all account data complete. Elapsed: {elapsed.TotalSeconds}s");
        dynamic context = new ExpandoObject();
        var contextDict = (IDictionary<string, object>)context;
        contextDict.Add("AccountsUpdated", accountsUpdated);
        contextDict.Add("AccountsSkipped", accountsSkipped);
        contextDict.Add("Errors", errors);
        contextDict.Add("TimeElapsed", elapsed);
        SendCacheUpdateEvent(_log, this, requestorGuid, context, firstException);
    }

    private List<Account> GetAccounts(DeveloperId.DeveloperId developerId)
    {
        try
        {
            var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(VSAccountUri, developerId);
            var client = connection.GetClient<AccountHttpClient>();
            return client.GetAccountsByMemberAsync(memberId: connection.AuthorizedIdentity.Id).Result;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed querying for organizations.");
            return [];
        }
    }

    private List<TeamProjectReference> GetProjects(Account account, VssConnection connection)
    {
        try
        {
            // If the organization is disabled the connection will be null.
            if (connection is null)
            {
                _log.Information($"Account {account.AccountName} had a null connection, treating as disabled.");
                return [];
            }

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

    private List<GitRepository> GetGitRepositories(TeamProjectReference project, VssConnection connection)
    {
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();
            var repositories = gitClient.GetRepositoriesAsync(project.Id, false, false).Result;
            return [.. repositories];
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Failed getting repositories for project: {project.Name}");
        }

        return [];
    }

    private static void SendCacheUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception? ex)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Cache, requestor, context, ex);
    }
}
