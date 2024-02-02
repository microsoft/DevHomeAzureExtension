// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Drawing;
using System.Security.Authentication;
using AzureExtension.Helpers;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
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
    private AzureRepositoryHierarchy? _azureHierarchy;

    private const string _server = "server";

    private const string _organization = "organization";

    private const string _project = "project";

    private string _selectedServer;

    private string _selectedOrganization;

    private string _selectedProject;

    public string DisplayName => Resources.GetResource(@"RepositoryProviderDisplayName");

    public IRandomAccessStreamReference Icon
    {
        get; private set;
    }

    string[] IRepositoryData.GetSearchFieldNames => new string[3] { _server, _organization, _project };

    public RepositoryProvider(IRandomAccessStreamReference icon)
    {
        Icon = icon;
        _selectedServer = string.Empty;
        _selectedOrganization = string.Empty;
        _selectedProject = string.Empty;
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
    private List<IRepository> GetRepos(VssConnection connection, TeamProjectReference project, GitPullRequestSearchCriteria criteria, bool shouldCheckForPrs)
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
                    if (shouldCheckForPrs)
                    {
                        var pullRequests = gitClient.GetPullRequestsAsync(repo.Id, criteria).Result;

                        if (pullRequests.Count != 0)
                        {
                            reposToReturn.Add(new DevHomeRepository(repo));
                        }
                    }
                    else
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
        return GetRepositoriesAsync(new Dictionary<string, string>(), developerId);
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

    public IAsyncOperation<RepositoriesResult> GetRepositoriesAsync(IReadOnlyDictionary<string, string> searchTerms, IDeveloperId developerId)
    {
        var serverToUse = searchTerms.ContainsKey(_server) ? searchTerms[_server] : string.Empty;
        _selectedServer = serverToUse;

        var organizationToUse = searchTerms.ContainsKey(_organization) ? searchTerms[_organization] : string.Empty;
        _selectedOrganization = organizationToUse;

        var projectToUse = searchTerms.ContainsKey(_project) ? searchTerms[_project] : string.Empty;
        _selectedProject = projectToUse;

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

        var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
        if (!string.IsNullOrEmpty(_selectedOrganization))
        {
#pragma warning disable CA1309 // Use ordinal string comparison.  Organization names should match exactly.
            organizations = organizations.Where(x => x.AccountName.Equals(_selectedOrganization)).ToList();
#pragma warning restore CA1309 // Use ordinal string comparison
        }

        var options = new ParallelOptions()
        {
            // Let the program decide
            MaxDegreeOfParallelism = -1,
        };

        Dictionary<Organization, List<TeamProjectReference>> organizationsAndProjects = new Dictionary<Organization, List<TeamProjectReference>>();

        Parallel.ForEach(organizations, options, orgnization =>
        {
            var projects = _azureHierarchy.GetProjectsAsync(orgnization).Result;
            if (!string.IsNullOrEmpty(_selectedProject))
            {
#pragma warning disable CA1309 // Use ordinal string comparison.  Organization names should match exactly.
                projects = projects.Where(x => x.Name.Equals(_selectedProject)).ToList();
#pragma warning restore CA1309 // Use ordinal string comparison
            }

            if (projects.Count != 0)
            {
                organizationsAndProjects.Add(orgnization, projects);
            }
        });

        var reposToReturn = new List<IRepository>();

        // By now organizationAndProjects include all relevant information to get repos.
        Parallel.ForEach(organizationsAndProjects, options, organizationAndProject =>
        {
            try
            {
                // Making a connection can throw.
                var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(organizationAndProject.Key.AccountUri, azureDeveloperId);
                var criteria = new GitPullRequestSearchCriteria();
                criteria.CreatorId = connection.AuthorizedIdentity.Id;

                Parallel.ForEach(organizationAndProject.Value, options, project =>
                {
                    reposToReturn.AddRange(GetRepos(connection, project, criteria, true));
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

    public IReadOnlyList<string> GetSearchFieldNames()
    {
        return new List<string> { _server, _organization, _project };
    }

    public IAsyncOperation<IList<string>> GetValuesForField(string fieldName, IReadOnlyDictionary<string, string> fieldValues, IDeveloperId developerId)
    {
        // Get access token for ADO API calls.
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            return Task.Run(() =>
            {
                return new List<string>() as IList<string>;
            }).AsAsyncOperation();
        }

#pragma warning disable IDE0059 // Unnecessary assignment of a value.  Server isn't needed until users use on prem.
        var serverToUse = fieldValues.ContainsKey(_server) ? fieldValues[_server] : string.Empty;
#pragma warning restore IDE0059 // Unnecessary assignment of a value

        var organizationToUse = fieldValues.ContainsKey(_organization) ? fieldValues[_organization] : string.Empty;
        var projectToUse = fieldValues.ContainsKey(_project) ? fieldValues[_project] : string.Empty;

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

#pragma warning disable CA1309 // Use ordinal string comparison.  Make sure names match linguisticly.
        if (fieldName.Equals(_organization))
        {
            var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
            if (!string.IsNullOrEmpty(organizationToUse))
            {
                organizations = organizations.Where(x => x.AccountName.Equals(organizationToUse)).ToList();
            }

            if (string.IsNullOrEmpty(projectToUse))
            {
                return Task.Run(() =>
                {
                    return organizations.Select(x => x.AccountName).ToList() as IList<string>;
                }).AsAsyncOperation();
            }
            else
            {
                var organizationsToReturn = new List<string>();
                foreach (var organization in organizations)
                {
                    var projects = _azureHierarchy.GetProjectsAsync(organization).Result.Where(x => x.Name.Equals(projectToUse)).ToList();
                    if (projects.Count > 0)
                    {
                        organizationsToReturn.Add(organization.AccountName);
                    }
                }

                return Task.Run(() =>
                {
                    return organizationsToReturn as IList<string>;
                }).AsAsyncOperation();
            }
        }
        else if (fieldName.Equals(_project))
        {
            // Because projects exist in an organization searching behavior is different for each combonation.
            var isOrganizationInInput = !string.IsNullOrEmpty(organizationToUse);
            var isProjectInInput = !string.IsNullOrEmpty(projectToUse);

            if (isOrganizationInInput && !isProjectInInput)
            {
                // get all projects in the organization.
                var organizations = _azureHierarchy.GetOrganizationsAsync().Result.Where(x => x.AccountName.Equals(organizationToUse)).ToList();
                List<string> projectNames = new List<string>();
                foreach (var organization in organizations)
                {
                    projectNames.AddRange(_azureHierarchy.GetProjectsAsync(organization).Result.Select(x => x.Name));
                }

                return Task.Run(() =>
                {
                    return projectNames as IList<string>;
                }).AsAsyncOperation();
            }

            if (isProjectInInput && !isOrganizationInInput)
            {
                // Get all projects with the same name in all organizations.
                // This does mean the project drop down might have duplicate names.
                var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
                List<string> projectNames = new List<string>();
                foreach (var organization in organizations)
                {
                    projectNames.AddRange(_azureHierarchy.GetProjectsAsync(organization).Result.Where(x => x.Name.Equals(projectToUse)).Select(x => x.Name));
                }

                return Task.Run(() =>
                {
                    return projectNames as IList<string>;
                }).AsAsyncOperation();
            }

            if (isOrganizationInInput && isProjectInInput)
            {
                // Get the organization.  If the organization does not exist, return nothing.
                // If the organization has the project, return the project name.
                // otherwise, return nothing.
                var organizations = _azureHierarchy.GetOrganizationsAsync().Result.Where(x => x.AccountName.Equals(organizationToUse)).ToList();

                List<string> projectNames = new List<string>();
                foreach (var organization in organizations)
                {
                    projectNames.AddRange(_azureHierarchy.GetProjectsAsync(organization).Result.Select(x => x.Name));
                }

                return Task.Run(() =>
                {
                    return projectNames as IList<string>;
                }).AsAsyncOperation();
            }
        }

        return Task.Run(() =>
        {
            return new List<string>() as IList<string>;
        }).AsAsyncOperation();
#pragma warning restore CA1309 // Use ordinal string comparison
    }

    public string? GetFieldSearchValue(string field, IDeveloperId developerId)
    {
#pragma warning disable CA1309 // Use ordinal string comparison.  Make sure names match linguisticly.
        if (field.Equals(_server))
        {
            return _selectedServer;
        }
        else if (field.Equals(_organization))
        {
            return _selectedOrganization;
        }
        else if (field.Equals(_project))
        {
            return _selectedProject;
        }
        else
        {
            return null;
        }
#pragma warning restore CA1309 // Use ordinal string comparison
    }

    public string GetDefaultValueFor(string field, IDeveloperId developerId)
    {
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            throw new NotSupportedException("Authenticated user is not an azure developer id");
        }

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

        var organizations = _azureHierarchy.GetOrganizationsAsync().Result;

        if (field.Equals(_server, StringComparison.OrdinalIgnoreCase))
        {
            if (organizations.FirstOrDefault(x => x.AccountUri.OriginalString.Contains("dev.azure.com")) != null)
            {
                return "dev.azure.com";
            }
            else
            {
                var organization = organizations[0];
                return $"{organization.AccountName}.visualstudio.com";
            }
        }

        if (field.Equals(_organization, StringComparison.OrdinalIgnoreCase))
        {
            var maybeNewOrganization = organizations.FirstOrDefault(x => x.AccountUri.OriginalString.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase));
            if (maybeNewOrganization != null)
            {
                return maybeNewOrganization.AccountName;
            }
            else
            {
                return organizations[0].AccountName;
            }
        }

        return string.Empty;
    }
}
