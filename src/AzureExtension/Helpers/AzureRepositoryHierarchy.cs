// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Collections.Concurrent;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Organization;
using Microsoft.VisualStudio.Services.WebApi;

// In the past, an organization was known as an account.  Typedef to organization to make the code easier to read.
using Organization = Microsoft.VisualStudio.Services.Account.Account;

namespace AzureExtension.Helpers;

/// <summary>
/// Handles the hierarchy between organizations and projects.  Handles querying for both as well.
/// </summary>
public class AzureRepositoryHierarchy
{
    private readonly ConcurrentDictionary<Organization, List<TeamProjectReference>> _organizationsAndProjects;

    /// <summary>
    /// Used to keep track of multiple Get requests to make sure work isn't duplicated.
    /// </summary>
    private readonly ConcurrentDictionary<Organization, Task<IPagedList<TeamProjectReference>>> _organizationsAndProjectTask;

    private readonly DeveloperId _developerId;

    /// <summary>
    /// Used to keep track of all GetOrganization requests to make sure work isn't duplicated.
    /// </summary>
    private Task<List<Organization>>? _queryOrganizationsTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureRepositoryHierarchy"/> class.
    /// Class to handle searching organizations and projects within a server.
    /// </summary>
    /// <param name="developerId">A developerId to an ADO instance.</param>
    /// <remarks>
    /// A server can have multiple organizations and each organization can have multiple projects.
    /// Additionally, organizations and projects need to be fetched from the network.
    /// This calss handles fetching the data, caching it, and searching it.
    /// </remarks>
    public AzureRepositoryHierarchy(DeveloperId developerId)
    {
        _developerId = developerId;
        _organizationsAndProjects = new ConcurrentDictionary<Organization, List<TeamProjectReference>>();
        _organizationsAndProjectTask = new ConcurrentDictionary<Organization, Task<IPagedList<TeamProjectReference>>>();
    }

    /// <summary>
    /// Get all organizations the is apart of.
    /// </summary>
    /// <returns>A list of all organizations.</returns>
    public async Task<List<Organization>> GetOrganizationsAsync()
    {
        // if something is running the query, wait for it to finish.
        if (_queryOrganizationsTask != null)
        {
            await _queryOrganizationsTask;
            return _organizationsAndProjects.Keys.ToList();
        }

        var organizations = QueryForOrganizations();
        foreach (var organization in organizations)
        {
            // Extra protection against duplicates if two threads run QueryForOrganizations at the same time.
            var duplicateOrganization = _organizationsAndProjects.Keys.FirstOrDefault(x => x.AccountId == organization.AccountId);
            if (duplicateOrganization == null)
            {
                _organizationsAndProjects.TryAdd(organization, new List<TeamProjectReference>());
            }
        }

        return _organizationsAndProjects.Keys.ToList();
    }

    /// <summary>
    /// Get all projects the user is apart of.
    /// </summary>
    /// <param name="organization">Filters down the returned list to those under organization</param>
    /// <returns>A list of projects.</returns>
    public async Task<List<TeamProjectReference>> GetProjectsAsync(Organization organization)
    {
        // if something is running the query, wait for it to finish.
        if (_queryOrganizationsTask != null)
        {
            await _queryOrganizationsTask;
        }

        // if the task has been started
        if (_organizationsAndProjectTask.TryGetValue(organization, out var projectsTask))
        {
            if (projectsTask != null)
            {
                // Wait for it.
                await projectsTask;
                return _organizationsAndProjects[organization];
            }
        }

        var projects = QueryForProjects(organization);
        _organizationsAndProjects[organization] = projects;

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
            _queryOrganizationsTask = accountClient.GetAccountsByMemberAsync(
                memberId: accountConnection.AuthorizedIdentity.Id);
            return _queryOrganizationsTask.Result;
        }
        catch
        {
            return new List<Organization>();
        }
    }

    /// <summary>
    /// Contacts the server to get all projects in an organization.
    /// </summary>
    /// <param name="organization">PRojects are returned only for this organization.</param>
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
                var getProjectsTask = projectClient.GetProjects();

                // Add the task if it does not yet exist.
                _organizationsAndProjectTask.TryAdd(organization, getProjectsTask);

                // in both cases, wait for the task to finish.
                return _organizationsAndProjectTask[organization].Result.ToList();
            }
        }
        catch (Exception e)
        {
            DevHomeAzureExtension.Client.Log.Logger()?.ReportError("DevHomeRepository", e);
        }

        return new List<TeamProjectReference>();
    }
}
