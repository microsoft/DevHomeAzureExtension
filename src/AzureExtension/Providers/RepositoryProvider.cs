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

public class RepositoryProvider : IRepositoryProvider, IRepositoryProvider2
{
    /// <summary>
    /// Used for singleton instance.
    /// </summary>
    private static readonly object _constructorLock = new();

    private static RepositoryProvider? _repositoryProvider;

    private AzureRepositoryHierarchy? _azureHierarchy;

    private const string _defaultSearchServerName = "dev.azure.com";

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

    string[] IRepositoryProvider2.GetSearchFieldNames => new string[3] { _server, _organization, _project };

    /// <summary>
    /// On first query, get repos based on the PR date.  All other requests won't check for PRs.
    /// </summary>
    private bool _shouldIgnorePRDate;

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

    public static RepositoryProvider GetInstance()
    {
        lock (_constructorLock)
        {
            _repositoryProvider ??= new RepositoryProvider();
        }

        return _repositoryProvider;
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
    /// Get all repositories under a porject.
    /// </summary>
    /// <param name="organizationConnection">The connection to the organization.</param>
    /// <param name="project">The project to get the repos for</param>
    /// <param name="criteria">Holds the user ID.</param>
    /// <param name="shouldIgnorePRDate">If criteria should be used or not.</param>
    /// <returns>A list of IRepository's.</returns>
    private List<IRepository> GetRepos(VssConnection organizationConnection, TeamProjectReference project, GitPullRequestSearchCriteria criteria, bool shouldIgnorePRDate)
    {
        Log.Logger()?.ReportInfo("DevHomeRepository", $"Getting all repos for {project.Name}");
        try
        {
            var gitClient = organizationConnection.GetClient<GitHttpClient>();
            var repos = gitClient.GetRepositoriesAsync(project.Id, false, false).Result.OrderBy(x => x.Name);

            var reposToReturn = new List<IRepository>();
            foreach (var repo in repos)
            {
                try
                {
                    if (shouldIgnorePRDate)
                    {
                        reposToReturn.Add(new DevHomeRepository(repo));
                    }
                    else
                    {
                        var pullRequests = gitClient.GetPullRequestsAsync(repo.Id, criteria).Result;

                        if (pullRequests.Count != 0)
                        {
                            reposToReturn.Add(new DevHomeRepository(repo));
                        }
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

    /// <summary>
    /// Get repositories for the developer.
    /// </summary>
    /// <param name="developerId">The account to use to get repositories.</param>
    /// <returns>A list of repositories the user has access to.</returns>
    public IAsyncOperation<RepositoriesResult> GetRepositoriesAsync(IDeveloperId developerId)
    {
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            return Task.Run(() =>
            {
                var exception = new NotSupportedException("Authenticated user is not the an azure developer id.");
                return new RepositoriesResult(exception, $"{exception.Message} HResult: {exception.HResult}");
            }).AsAsyncOperation();
        }

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

        var server = string.Empty;
        var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
#pragma warning disable CA1309 // Use ordinal string comparison
        // Try to find any organizations with the modern URL format.
        var defaultOrg = organizations.FirstOrDefault(x => x.AccountName.Equals(_defaultSearchServerName));
#pragma warning restore CA1309 // Use ordinal string comparison
        if (defaultOrg != null)
        {
            server = _defaultSearchServerName;
        }

        return GetRepositoriesAsync(new Dictionary<string, string> { { _server, server } }, developerId);
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
        // First query will find repos only if the user made a PR.
        var shouldIgnorePRDateOld = _shouldIgnorePRDate;
        _shouldIgnorePRDate = true;
        return Task.Run(() =>
        {
            try
            {
                // Store the search terms.
                var serverToUse = searchTerms.ContainsKey(_server) ? searchTerms[_server] : string.Empty;
                _selectedServer = serverToUse;

                var organizationToUse = searchTerms.ContainsKey(_organization) ? searchTerms[_organization] : string.Empty;
                _selectedOrganization = organizationToUse;

                var projectToUse = searchTerms.ContainsKey(_project) ? searchTerms[_project] : string.Empty;
                _selectedProject = projectToUse;

                // Get access token for ADO API calls.
                if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
                {
                    var exception = new NotSupportedException("Authenticated user is not the an azure developer id.");
                    return new RepositoriesResult(exception, $"{exception.Message} HResult: {exception.HResult}");
                }

                _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

#pragma warning disable CA1309 // Use ordinal string comparison.  Organization names should match exactly.
                var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
                var orgsInModernUrlFormat = new List<Organization>();
                if (_selectedServer.Equals(_defaultSearchServerName))
                {
                    // For a default search, look up all orgs with the modern URL format
                    orgsInModernUrlFormat = organizations.Where(x => x.AccountUri.Host.Equals(_defaultSearchServerName)).ToList();
                }

                // If the user is apart of orgs in the modern URL format, use these going forward.
                if (orgsInModernUrlFormat.Count > 0)
                {
                    organizations = orgsInModernUrlFormat;
                }

                if (!string.IsNullOrEmpty(_selectedOrganization))
                {
                    organizations = organizations.Where(x => x.AccountName.Equals(_selectedOrganization)).ToList();
                }
#pragma warning restore CA1309 // Use ordinal string comparison

                var options = new ParallelOptions()
                {
                    // Let the program decide
                    MaxDegreeOfParallelism = -1,
                };

                Dictionary<Organization, List<TeamProjectReference>> organizationsAndProjects = new Dictionary<Organization, List<TeamProjectReference>>();

                // Establish a connection between organizations and their projects.
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

                // organizationAndProjects include all relevant information to get repos.
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
                            reposToReturn.AddRange(GetRepos(connection, project, criteria, shouldIgnorePRDateOld));
                        });
                    }
                    catch (Exception e)
                    {
                        Providers.Log.Logger()?.ReportError("DevHomeRepository", e);
                    }
                });

                return new RepositoriesResult(reposToReturn);
            }
            catch (Exception ex)
            {
                Providers.Log.Logger()?.ReportError("DevHomeRepository", ex);
                return new RepositoriesResult(new List<IRepository>());
            }
        }).AsAsyncOperation();
    }

    /// <summary>
    /// Returns a list of field names this will accept as search input.
    /// </summary>
    /// <returns>A readonly list of search field names.</returns>
    public IReadOnlyList<string> GetSearchFieldNames()
    {
        return new List<string> { _server, _organization, _project };
    }

    /// <summary>
    /// Gets a list of values that work with the given input.
    /// </summary>
    /// <param name="fieldName">Results will be for this search field.</param>
    /// <param name="fieldValues">The inputs used to filter the results.</param>
    /// <param name="developerId">Used to access private org and project information.</param>
    /// <returns>A list of all values for fieldName that makes snes given the input.</returns>
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

        return Task.Run(() =>
        {
#pragma warning disable IDE0059 // Unnecessary assignment of a value.  Server isn't needed until users use on prem.
            var serverToUse = fieldValues.ContainsKey(_server) ? fieldValues[_server] : string.Empty;
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            var organizationToUse = fieldValues.ContainsKey(_organization) ? fieldValues[_organization] : string.Empty;
            var projectToUse = fieldValues.ContainsKey(_project) ? fieldValues[_project] : string.Empty;

            _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

#pragma warning disable CA1309 // Use ordinal string comparison.  Make sure names match linguisticly.
            if (fieldName.Equals(_organization))
            {
                // Requesting organizations and an organization is given.
                var organizations = _azureHierarchy.GetOrganizationsAsync().Result;
                if (!string.IsNullOrEmpty(organizationToUse))
                {
                    organizations = organizations.Where(x => x.AccountName.Equals(organizationToUse)).OrderBy(x => x.AccountName).ToList();
                }

                // If no projects are in the input, return the organizations.
                if (string.IsNullOrEmpty(projectToUse))
                {
                    return organizations.Select(x => x.AccountName).Order().ToList() as IList<string>;
                }
                else
                {
                    // Find all organizations with the given project name.
                    var organizationsToReturn = new List<string>();
                    foreach (var organization in organizations)
                    {
                        var projects = _azureHierarchy.GetProjectsAsync(organization).Result.Where(x => x.Name.Equals(projectToUse)).ToList();
                        if (projects.Count > 0)
                        {
                            organizationsToReturn.Add(organization.AccountName);
                        }
                    }

                    return organizationsToReturn.Order().ToList() as IList<string>;
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

                    return projectNames.Order().ToList() as IList<string>;
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

                    return projectNames.Order().ToList() as IList<string>;
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

                    return projectNames.Order().ToList() as IList<string>;
                }
            }

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

    /// <summary>
    /// Used to return terms used for a default search.
    /// In this case server is dev.azure.com if any organizations use the modern URL format.  Otherwise it is the old URL format.
    /// Organization is the first one found.
    /// </summary>
    /// <param name="field">The search field to search for.</param>
    /// <param name="developerId">Used to get server and organization information.</param>
    /// <returns>A default search value.</returns>
    public string GetDefaultValueFor(string field, IDeveloperId developerId)
    {
        if (developerId is not DeveloperId.DeveloperId azureDeveloperId)
        {
            Providers.Log.Logger()?.ReportError("DevHomeRepository", "Authenticated user is not an azure developer id");
            return string.Empty;
        }

        _azureHierarchy ??= new AzureRepositoryHierarchy(azureDeveloperId);

        var organizations = _azureHierarchy.GetOrganizationsAsync().Result;

        // Get a default server name.
        if (field.Equals(_server, StringComparison.OrdinalIgnoreCase))
        {
            if (organizations.FirstOrDefault(x => x.AccountUri.OriginalString.Contains("dev.azure.com")) != null)
            {
                return "dev.azure.com";
            }
            else
            {
                if (organizations.Count != 0)
                {
                    var organization = organizations[0];
                    return $"{organization.AccountName}.visualstudio.com";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        // get the default organization
        if (field.Equals(_organization, StringComparison.OrdinalIgnoreCase))
        {
            var maybeNewOrganization = organizations.FirstOrDefault(x => x.AccountUri.OriginalString.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase));
            if (maybeNewOrganization != null)
            {
                return maybeNewOrganization.AccountName;
            }
            else
            {
                if (organizations.Count != 0)
                {
                    return organizations[0].AccountName;
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
