// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Security.Authentication;
using System.Text;
using AzureExtension.Helpers;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Azure.Pipelines.WebApi;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using Windows.Storage.Streams;

// In the past, an organization was known as an account.  Typedef to organization to make the code easier to read.
using Organization = Microsoft.VisualStudio.Services.Account.Account;

namespace DevHomeAzureExtension.Providers;

public class RepositoryProvider : IRepositoryProvider, IRepositoryProvider2, IRepositoryData
{
    private Dictionary<Organization, List<TeamProjectReference>> _organizationsToProjects;

    private AzureRepositoryHierarchy? _azureHierarchy;

    private const string _orgnization = "Organization";

    private const string _project = "Project";

    private string _organizationName = string.Empty;

    private string _projectName = string.Empty;

    public string DisplayName => Resources.GetResource(@"RepositoryProviderDisplayName");

    public IRandomAccessStreamReference Icon
    {
        get; private set;
    }

    public RepositoryProvider(IRandomAccessStreamReference icon)
    {
        Icon = icon;
        _organizationsToProjects = new Dictionary<Organization, List<TeamProjectReference>>();
    }

    public RepositoryProvider()
        : this(RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///AzureExtension/Assets/AzureExtensionDark.png")))
    {
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
                return new RepositoryUriSupportResult(new ArgumentException("Not a valid Azure Devops Uri"), "Uri isn not valid");
            }

            if (!azureUri.IsRepository)
            {
                return new RepositoryUriSupportResult(new ArgumentException("Uri does not point to a repository"), "Uri does not point to a repository");
            }

            return new RepositoryUriSupportResult(true);
        }).AsAsyncOperation();
    }

    /// <summary>
    /// Gets all repos from the project that the user did a pull request into
    /// </summary>
    private List<IRepository> GetRepos(VssConnection connection, TeamProjectReference project, GitPullRequestSearchCriteria criteria)
    {
        Log.Logger()?.ReportInfo("DevHomeRepository", $"Getting all repos for {project.Name}");
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();
            var repos = gitClient.GetRepositoriesAsync(project.Id, true, true).Result.OrderBy(x => x.Name);

            var reposToReturn = new List<IRepository>();
            foreach (var repo in repos)
            {
                try
                {
                    var pullRequests = gitClient.GetPullRequestsAsync(repo.Id, criteria).Result;

                    if (pullRequests.Count != 0)
                    {
                        reposToReturn.Add(new DevHomeRepository(repo));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return reposToReturn;
        }
        catch
        {
            return new List<IRepository>();
        }
    }

    public IAsyncOperation<RepositoriesResult> GetRepositoriesAsync(IDeveloperId developerId)
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

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

        var reposToReturn = new List<IRepository>();

        var options = new ParallelOptions()
        {
            // Considering these are API calls, having more degrees than Environment.CoreCount should be okay.
            MaxDegreeOfParallelism = -1,
        };

        Parallel.ForEach(_azureHierarchy.GetOrganizations(), options, orgnization =>
        {
            try
            {
                // Making a connection can throw.
                var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(orgnization.AccountUri, azureDeveloperId);
                var criteria = new GitPullRequestSearchCriteria();
                criteria.CreatorId = connection.AuthorizedIdentity.Id;

                Parallel.ForEach(_azureHierarchy.GetProjects(orgnization.AccountName), options, project =>
                {
                    reposToReturn.AddRange(GetRepos(connection, project, criteria));
                });
            }
            catch (Exception e)
            {
                Providers.Log.Logger()?.ReportError("DevHomeRepository", e);
            }
        });

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
                    var exception = new NotSupportedException("Uri isn't a valid Azure Devops uri.");
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

    public IEnumerable<IRepository> GetRepositoriesAsync(IDeveloperId developerId, IDictionary<string, string> searchTerms)
    {
        if (searchTerms.ContainsKey(_orgnization))
        {
            _organizationName = searchTerms[_organizationName];
        }
        else
        {
            _organizationName = string.Empty;
        }

        if (searchTerms.ContainsKey(_project))
        {
            _projectName = searchTerms[_project];
        }
        else
        {
            _projectName = string.Empty;
        }

        var foundRepositoriesAwaiter = GetRepositoriesAsync(developerId).GetAwaiter();

        while (!foundRepositoriesAwaiter.IsCompleted)
        {
            Thread.Sleep(1000);
        }

        var foundRepositories = foundRepositoriesAwaiter.GetResult().Repositories;
        return foundRepositories;
    }

    public IReadOnlyList<string> GetSearchFieldNames()
    {
        return new List<string> { "Organization", "Project" };
    }

    public IReadOnlyList<string> GetValuesBasedOn(IDictionary<string, string> inputValues, string fieldNameToRetrieve, IDeveloperId developerId)
    {
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            throw new NotSupportedException("Authenticated user is not an azure developer id");
        }

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

        if (fieldNameToRetrieve.Equals(_orgnization, StringComparison.OrdinalIgnoreCase))
        {
            var inputProject = string.Empty;
            if (inputValues.ContainsKey(_project))
            {
                inputProject = inputValues[_project];
            }

            return _azureHierarchy.GetOrganizations(inputProject).Select(x => x.AccountName).ToList();
        }
        else if (fieldNameToRetrieve.Equals(_project, StringComparison.OrdinalIgnoreCase))
        {
            var inputOrganization = string.Empty;
            if (inputValues.ContainsKey(_orgnization))
            {
                inputOrganization = inputValues[_orgnization];
            }

            return _azureHierarchy.GetProjects(inputOrganization).Select(x => x.Name).ToList();
        }

        return new List<string>();
    }
}
