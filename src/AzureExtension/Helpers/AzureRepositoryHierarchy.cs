// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;

// In the past, an organization was known as an account.  Typedef to organization to make the code easier to read.
using Organization = Microsoft.VisualStudio.Services.Account.Account;

namespace DevHomeAzureExtension.Helpers;

/// <summary>
/// Handles the hierarchy between organizations and projects.  Handles querying for both as well.
/// </summary>
public class AzureRepositoryHierarchy
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(AzureRepositoryHierarchy)));

    private static readonly ILogger _log = _logger.Value;

    private readonly object _getOrganizationsLock = new();

    private readonly object _getProjectsLock = new();

    // 1:N Organization to project.
    private readonly Dictionary<Organization, List<TeamProjectReference>> _organizationsAndProjects = new();

    private readonly DeveloperId.DeveloperId _developerId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureRepositoryHierarchy"/> class.
    /// Class to handle searching organizations and projects within a server.
    /// </summary>
    /// <param name="developerId">A developerId to an ADO instance.</param>
    /// <remarks>
    /// A server can have multiple organizations and each organization can have multiple projects.
    /// Additionally, organizations and projects need to be fetched from the network.
    /// This class handles fetching the data, caching it, and searching it.
    /// </remarks>
    public AzureRepositoryHierarchy(DeveloperId.DeveloperId developerId)
    {
        _developerId = developerId;
    }

    /// <summary>
    /// Get all organizations the user is apart of.
    /// </summary>
    /// <returns>A list of all organizations.</returns>
    public List<Organization> GetOrganizations()
    {
        lock (_getOrganizationsLock)
        {
            if (_organizationsAndProjects.Keys.Count == 0)
            {
                var organizations = QueryForOrganizations();
                foreach (var organization in organizations)
                {
                    _organizationsAndProjects.TryAdd(organization, new List<TeamProjectReference>());
                }
            }

            return _organizationsAndProjects.Keys.ToList();
        }
    }

    /// <summary>
    /// Get all projects the user is apart of.
    /// </summary>
    /// <param name="organization">Filters down the returned list to those under organization</param>
    /// <returns>A list of projects.</returns>
    public async Task<List<TeamProjectReference>> GetProjectsAsync(Organization organization)
    {
        // Makes sure _organizationsAndProjects has all organizations.
        await Task.Run(() => GetOrganizations());

        _organizationsAndProjects.TryGetValue(organization, out var projects);

        if (projects == null || projects.Count == 0)
        {
            lock (_getProjectsLock)
            {
                // Double check against race conditions.
                if (projects == null || projects.Count == 0)
                {
                    _organizationsAndProjects[organization] = QueryForProjects(organization);
                }
            }
        }

        return _organizationsAndProjects[organization];
    }

    /// <summary>
    /// Contacts the server to get all organizations the user is apart of.
    /// </summary>
    /// <returns>A list of organizations.</returns>
    /// <remarks>
    /// The returned list does include disabled organizations.
    /// _queryOrganizationsTask is set here.
    /// </remarks>
    private List<Organization> QueryForOrganizations()
    {
        // Establish a connection to get all organizations.
        // The library calls these "accounts".
        VssConnection accountConnection;

        try
        {
            accountConnection = AzureClientProvider.GetConnectionForLoggedInDeveloper(new Uri(@"https://app.vssps.visualstudio.com/"), _developerId);
        }
        catch
        {
            return new List<Organization>();
        }

        var accountClient = accountConnection.GetClient<AccountHttpClient>();

        try
        {
            return accountClient.GetAccountsByMemberAsync(
                memberId: accountConnection.AuthorizedIdentity.Id).Result;
        }
        catch
        {
            return new List<Organization>();
        }
    }

    /// <summary>
    /// Contacts the server to get all projects in an organization.
    /// </summary>
    /// <param name="organization">Projects are returned only for this organization.</param>
    /// <returns>A list of projects.</returns>
    /// <remarks>
    /// the Task to get the projects is added to _organizationsAndProjectTask.
    /// </remarks>
    private List<TeamProjectReference> QueryForProjects(Organization organization)
    {
        try
        {
            var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(organization.AccountUri, _developerId);

            // connection can be null if the organization is disabled.
            if (connection != null)
            {
                var projectClient = connection.GetClient<ProjectHttpClient>();
                var projects = projectClient.GetProjects().Result.ToList();
                _organizationsAndProjects[organization] = projects;

                // in both cases, wait for the task to finish.
                return projects;
            }
        }
        catch (Exception e)
        {
            _log.Error(e, e.Message);
        }

        return new List<TeamProjectReference>();
    }
}
