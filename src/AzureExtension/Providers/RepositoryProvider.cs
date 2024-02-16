// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Text;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace DevHomeAzureExtension.Providers;

public class RepositoryProvider : IRepositoryProvider
{
    public string DisplayName => Resources.GetResource(@"RepositoryProviderDisplayName");

    public IRandomAccessStreamReference Icon
    {
        get; private set;
    }

    public RepositoryProvider(IRandomAccessStreamReference icon)
    {
        Icon = icon;
    }

    public RepositoryProvider()
    {
        Icon = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///AzureExtension/Assets/AzureExtensionDark.png"));
    }

    public IAsyncOperation<RepositoryUriSupportResult> IsUriSupportedAsync(Uri uri)
    {
        return IsUriSupportedAsync(uri, null);
    }

    public IAsyncOperation<RepositoryUriSupportResult> IsUriSupportedAsync(Uri uri, IDeveloperId? developerId)
    {
        return Task.Run(() =>
        {
            var azureUri = new AzureUri(uri);
            if (!azureUri.IsValid)
            {
                return new RepositoryUriSupportResult(new ArgumentException("Not a valid Azure DevOps Uri"), "Uri is not valid");
            }

            if (!azureUri.IsRepository)
            {
                return new RepositoryUriSupportResult(new ArgumentException("Uri does not point to a repository"), "Uri does not point to a repository");
            }

            return new RepositoryUriSupportResult(true);
        }).AsAsyncOperation();
    }

    /// <summary>
    /// Get all projects for an organization.
    /// </summary>
    /// <param name="organization">The org to look under.</param>
    /// <param name="azureDeveloperId">The developerId to get the connection.</param>
    /// <returns>A list of projects the organization has.</returns>
    private List<TeamProjectReference> GetProjects(Microsoft.VisualStudio.Services.Account.Account organization, DeveloperId.DeveloperId azureDeveloperId)
    {
        try
        {
            var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(organization.AccountUri, azureDeveloperId);

            // connection can be null if the organization is disabled.
            if (connection != null)
            {
                var projectClient = connection.GetClient<ProjectHttpClient>();
                return projectClient.GetProjects().Result.ToList();
            }
        }
        catch (Exception e)
        {
            Providers.Log.Logger()?.ReportError("DevHomeRepository", e);
        }

        return new List<TeamProjectReference>();
    }

    IAsyncOperation<RepositoriesResult> IRepositoryProvider.GetRepositoriesAsync(IDeveloperId developerId)
    {
        // Get access token for ADO API calls.
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            return Task.Run(() =>
            {
                var exception = new NotSupportedException("Authenticated user is not the an azure developer id.");
                return new RepositoriesResult(exception, $"{exception.Message} HResult: {exception.HResult}");
            }).AsAsyncOperation();
        }

        // Establish a connection to get all organizations.
        // The library calls these organizations "accounts".
        VssConnection accountConnection;

        try
        {
            accountConnection = AzureClientProvider.GetConnectionForLoggedInDeveloper(new Uri(@"https://app.vssps.visualstudio.com/"), azureDeveloperId);
        }
        catch (Exception e)
        {
            return Task.Run(() =>
            {
                return new RepositoriesResult(e, "Could not establish a connection for this account");
            }).AsAsyncOperation();
        }

        var accountClient = accountConnection.GetClient<AccountHttpClient>();

        // Get all organizations the current user belongs to.
        var internalDevId = DeveloperIdProvider.GetInstance().GetDeveloperIdInternal(developerId);
        IReadOnlyList<Microsoft.VisualStudio.Services.Account.Account>? theseOrganizations;

        try
        {
            theseOrganizations = accountClient.GetAccountsByMemberAsync(
                memberId: accountConnection.AuthorizedIdentity.Id).Result;
        }
        catch (Exception e)
        {
            return Task.Run(() =>
            {
                return new RepositoriesResult(e, "Could not get the member id for this user.");
            }).AsAsyncOperation();
        }

        // Set up parallel options to get all projects for each organization.
        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        var projectsWithOrgName = new List<(TeamProjectReference, Microsoft.VisualStudio.Services.Account.Account)>();

        try
        {
            Parallel.ForEach(theseOrganizations, options, organization =>
            {
                var projects = GetProjects(organization, azureDeveloperId);
                if (projects.Count != 0)
                {
                    foreach (var project in projects)
                    {
                        projectsWithOrgName.Add((project, organization));
                    }
                }
            });
        }
        catch (AggregateException aggregateException)
        {
            var exceptionMessages = new StringBuilder();

            foreach (var exceptionMessage in aggregateException.InnerExceptions)
            {
                exceptionMessages.AppendLine(exceptionMessage.Message);
            }

            return Task.Run(() =>
            {
                return new RepositoriesResult(aggregateException, exceptionMessages.ToString());
            }).AsAsyncOperation();
        }
        catch (Exception e)
        {
            return Task.Run(() =>
            {
                return new RepositoriesResult(e, e.Message);
            }).AsAsyncOperation();
        }

        projectsWithOrgName = projectsWithOrgName.OrderByDescending(x => x.Item1.LastUpdateTime).ToList();

        // Get a list of the 200 most recently updated repos.
        var reposToReturn = new List<IRepository>();
        foreach (var projectWithOrgName in projectsWithOrgName)
        {
            // Hard limit on 200.
            // TODO: Figure out a better way to limit results, or, at least, pagination.
            if (reposToReturn.Count >= 200)
            {
                break;
            }

            try
            {
                // Making a connection can throw.
                var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(projectWithOrgName.Item2.AccountUri, azureDeveloperId);

                // Make the GitHttpClient inside try/catch because an exception happens if the project is disabled.
                var gitClient = connection.GetClient<GitHttpClient>();
                var repos = gitClient.GetRepositoriesAsync(projectWithOrgName.Item1.Id, false, false).Result;
                foreach (var repo in repos)
                {
                    if (repo.IsDisabled.HasValue && !repo.IsDisabled.Value)
                    {
                        if (reposToReturn.Count >= 200)
                        {
                            break;
                        }

                        reposToReturn.Add(new DevHomeRepository(repo));
                    }
                }
            }
            catch (Exception e)
            {
                Providers.Log.Logger()?.ReportError("DevHomeRepository", e);
            }
        }

        return Task.Run(() =>
        {
            return new RepositoriesResult(reposToReturn);
        }).AsAsyncOperation();
    }

    public IAsyncOperation<RepositoryResult> GetRepositoryFromUriAsync(Uri uri)
    {
        return GetRepositoryFromUriAsync(uri, null);
    }

    public IAsyncOperation<RepositoryResult> GetRepositoryFromUriAsync(Uri uri, IDeveloperId? developerId)
    {
        return Task.Run(() =>
        {
            if (developerId == null)
            {
                var exception = new NotSupportedException("Cannot get a repo without an authenticated user");
                return new RepositoryResult(exception, $"{exception.Message} HResult: {exception.HResult}");
            }

            if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
            {
                var exception = new NotSupportedException("Authenticated user is not the an azure developer id.");
                return new RepositoryResult(exception, $"{exception.Message} HResult: {exception.HResult}");
            }

            try
            {
                var authResult = DeveloperIdProvider.GetInstance().GetAuthenticationResultForDeveloperId(azureDeveloperId);
                if (authResult == null)
                {
                    var exception = new AuthenticationException($"Could not get authentication for user {developerId.LoginId}");
                    return new RepositoryResult(exception, $"Something went wrong.  HResult: {exception.HResult}");
                }

                var repoInformation = new AzureUri(uri);
                if (!repoInformation.IsValid)
                {
                    var exception = new NotSupportedException("Uri isn't a valid Azure DevOps uri.");
                    return new RepositoryResult(exception, $"{exception.Message} HResult: {exception.HResult}");
                }

                if (!repoInformation.IsRepository)
                {
                    var exception = new NotSupportedException("Uri does not point to a repository.");
                    return new RepositoryResult(exception, $"{exception.Message} HResult: {exception.HResult}");
                }

                var connection = new VssConnection(repoInformation.OrganizationLink, new VssAadCredential(new VssAadToken("Bearer", authResult.AccessToken)));

                var gitClient = connection.GetClient<GitHttpClient>();
                var repo = gitClient.GetRepositoryAsync(repoInformation.Project, repoInformation.Repository).Result;
                if (repo == null)
                {
                    var exception = new LibGit2Sharp.NotFoundException("Could not find the repo.");
                    return new RepositoryResult(exception, $"Something went wrong.  HResult: {exception.HResult}");
                }

                return new RepositoryResult(new DevHomeRepository(repo));
            }
            catch (Exception e)
            {
                return new RepositoryResult(e, $"Could not make a repository object");
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ProviderOperationResult> CloneRepositoryAsync(IRepository repository, string cloneDestination)
    {
        return Task.Run(() =>
        {
            var exception = new NotImplementedException("Cannot clone without a developer id.");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, exception, $"{exception.Message} HResult: {exception.HResult}", exception.Message);
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ProviderOperationResult> CloneRepositoryAsync(IRepository repository, string cloneDestination, IDeveloperId? developerId)
    {
        if (developerId == null)
        {
            return Task.Run(() =>
            {
                var exception = new NotImplementedException("Developer ID is null.");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, exception, $"{exception.Message} HResult: {exception.HResult}", exception.Message);
            }).AsAsyncOperation();
        }

        return Task.Run(() =>
        {
            if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
            {
                var exception = new NotSupportedException("Authenticated user is not an azure developer id");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, exception, $"{exception.Message} HResult: {exception.HResult}", exception.Message);
            }

            AuthenticationResult? authResult;
            try
            {
                authResult = DeveloperIdProvider.GetInstance().GetAuthenticationResultForDeveloperId(azureDeveloperId);
                if (authResult == null)
                {
                    var exception = new NotImplementedException("Could not get authentication from developer id.");
                    return new ProviderOperationResult(ProviderOperationStatus.Failure, exception, $"Could not get authentication from developer id.  HResult: {exception.HResult}", exception.Message);
                }
            }
            catch (Exception e)
            {
                return new ProviderOperationResult(ProviderOperationStatus.Failure, e, $"Could not get authentication from developer id.  HResult: {e.HResult}", e.Message);
            }

            var cloneOptions = new LibGit2Sharp.CloneOptions
            {
                Checkout = true,
            };

            if (developerId != null)
            {
                DeveloperId.DeveloperId loggedInDeveloperId;
                try
                {
                    loggedInDeveloperId = DeveloperId.DeveloperIdProvider.GetInstance().GetDeveloperIdInternal(developerId);
                }
                catch (Exception e)
                {
                    return new ProviderOperationResult(ProviderOperationStatus.Failure, e, $"Could not get the logged in developer.  HResult: {e.HResult}", e.Message);
                }

                cloneOptions.FetchOptions.CredentialsProvider = (url, user, cred) => new LibGit2Sharp.UsernamePasswordCredentials
                {
                    // Password is a PAT unique to GitHub.
                    Username = authResult.AccessToken,
                    Password = string.Empty,
                };
            }

            try
            {
                // Exceptions happen.
                LibGit2Sharp.Repository.Clone(repository.RepoUri.OriginalString, cloneDestination, cloneOptions);
            }
            catch (LibGit2Sharp.RecurseSubmodulesException recurseException)
            {
                Log.Logger()?.ReportError("DevHomeRepository", "Could not clone all sub modules", recurseException);
                return new ProviderOperationResult(ProviderOperationStatus.Failure, recurseException, "Could not clone all modules", recurseException.Message);
            }
            catch (LibGit2Sharp.UserCancelledException userCancelledException)
            {
                Log.Logger()?.ReportError("DevHomeRepository", "The user stopped the clone operation", userCancelledException);
                return new ProviderOperationResult(ProviderOperationStatus.Failure, userCancelledException, "User cancelled the operation", userCancelledException.Message);
            }
            catch (LibGit2Sharp.NameConflictException nameConflictException)
            {
                Log.Logger()?.ReportError("DevHomeRepository", nameConflictException);
                return new ProviderOperationResult(ProviderOperationStatus.Failure, nameConflictException, "The location exists and is non-empty", nameConflictException.Message);
            }
            catch (LibGit2Sharp.LibGit2SharpException libGitTwoException)
            {
                Log.Logger()?.ReportError("DevHomeRepository", $"Either no logged in account has access to this repo, or the repo can't be found", libGitTwoException);
                return new ProviderOperationResult(ProviderOperationStatus.Failure, libGitTwoException, "LigGit2 threw an exception", "LibGit2 Threw an exception");
            }
            catch (Exception e)
            {
                Log.Logger()?.ReportError("DevHomeRepository", "Could not clone the repository", e);
                return new ProviderOperationResult(ProviderOperationStatus.Failure, e, "Something happened when cloning the repo", "something happened when cloning the repo");
            }

            return new ProviderOperationResult(ProviderOperationStatus.Success, new ArgumentException("Nothing wrong"), "Nothing wrong", "Nothing wrong");
        }).AsAsyncOperation();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
