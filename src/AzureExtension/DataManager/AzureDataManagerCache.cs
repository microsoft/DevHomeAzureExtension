// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Account;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace DevHomeAzureExtension;

public partial class AzureDataManager
{
    private static readonly TimeSpan AccountSyncTime = TimeSpan.FromDays(1);

    private static readonly Uri VSAccountUri = new(@"https://app.vssps.visualstudio.com/");

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task UpdateDataForAccountsAsync(bool force = false)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var developerIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIdsInternal();
        foreach (var developerId in developerIds)
        {
            _log.Debug($"Updating accounts for {developerId.LoginId}");
            var accounts = GetAccounts(developerId);
            foreach (var account in accounts)
            {
                var org = Organization.Get(DataStore, account.AccountUri.ToString());
                if (org is not null)
                {
                    if (!force && ((DateTime.Now - org.LastSyncAt) < AccountSyncTime))
                    {
                        _log.Information($"Organization: {org.Name} has recently been updated, skipping.");
                        continue;
                    }
                }

                // Transactional unit is the account (organization), which chains down to Project
                // and repository.
                using var tx = DataStore.Connection!.BeginTransaction();
                try
                {
                    var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(account.AccountUri, developerId);
                    _log.Debug($"Updating organization: {account.AccountName}");
                    var organization = Organization.GetOrCreate(DataStore, account.AccountUri);
                    var projects = GetProjects(account, connection);
                    foreach (var project in projects)
                    {
                        _log.Debug($"Updating project: {project.Name}");
                        var dsProject = Project.GetOrCreateByTeamProject(DataStore, project, organization.Id);
                        var repositories = GetGitRepositories(project, connection);
                        foreach (var repository in repositories)
                        {
                            _log.Debug($"Updating repository: {repository.Name}");
                            var dsRepository = Repository.GetOrCreate(DataStore, repository, dsProject.Id);
                        }
                    }

                    organization.SetSynced();
                    tx.Commit();
                    _log.Information($"Updated organization: {account.AccountName}");
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Unexpected failure updating account: {account.AccountName}");
                    tx.Rollback();
                }
            }
        }

        _log.Information("Updating all account data complete.");
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
}
