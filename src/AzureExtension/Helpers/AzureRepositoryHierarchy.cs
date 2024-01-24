// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Account.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

// In the past, an organization was known as an account.  Typedef to organization to make the code easier to read.
using Organization = Microsoft.VisualStudio.Services.Account.Account;

namespace AzureExtension.Helpers;

public class AzureRepositoryHierarchy
{
    private readonly DeveloperId _developerId;

    private readonly Task _buildingHierarchyTask;

    private List<KeyValuePair<Organization, List<TeamProjectReference>>> _organizationsToTheirProjects = new();

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
        _buildingHierarchyTask = BuildHierarchy();
    }

    public List<Organization> GetOrganizations(string? project = null)
    {
        Task.WaitAny(_buildingHierarchyTask);

        IEnumerable<KeyValuePair<Organization, List<TeamProjectReference>>> organizationsToReturn = _organizationsToTheirProjects;

        if (!string.IsNullOrEmpty(project))
        {
            organizationsToReturn = organizationsToReturn.Where(x => x.Value.Any(y => y.Name.Contains(project)));
        }

        return organizationsToReturn.Select(x => x.Key).ToList();
    }

    public List<TeamProjectReference> GetProjects(string? organization = null)
    {
        Task.WaitAny(_buildingHierarchyTask);

        IEnumerable<KeyValuePair<Organization, List<TeamProjectReference>>> projectsToReturn = _organizationsToTheirProjects;

        if (!string.IsNullOrEmpty(organization))
        {
            projectsToReturn = projectsToReturn.Where(x => x.Key.AccountName.Equals(organization, StringComparison.OrdinalIgnoreCase));
        }

        List<TeamProjectReference> projectNames = new();
        projectsToReturn.ForEach(x => projectNames.AddRange(x.Value));

        return projectNames;
    }

    private async Task BuildHierarchy()
    {
        await QueryForOrganizations();

        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = -1,
        };

        Parallel.ForEach(_organizationsToTheirProjects.Select(x => x.Key), options, async organization =>
        {
            var projects = await QueryForProjects(organization);

            // Only add the organization and projects if the organization hasn't been added yet.
            if (!_organizationsToTheirProjects.Select(x => x.Key).Contains(organization))
            {
                _organizationsToTheirProjects.Add(new KeyValuePair<Organization, List<TeamProjectReference>>(organization, projects));
            }
        });
    }

    private async Task<List<Organization>> QueryForOrganizations()
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
            return await accountClient.GetAccountsByMemberAsync(
                memberId: accountConnection.AuthorizedIdentity.Id, new List<string> { "organization" });
        }
        catch
        {
            return new List<Organization>();
        }
    }

    private async Task<List<TeamProjectReference>> QueryForProjects(Organization organization)
    {
        try
        {
            var connection = AzureClientProvider.GetConnectionForLoggedInDeveloper(organization.AccountUri, _developerId);

            // connection can be null if the organization is disabled.
            if (connection != null)
            {
                var projectClient = connection.GetClient<ProjectHttpClient>();
                return (await projectClient.GetProjects()).ToList();
            }
        }
        catch (Exception e)
        {
            DevHomeAzureExtension.Client.Log.Logger()?.ReportError("DevHomeRepository", e);
        }

        return new List<TeamProjectReference>();
    }
}
