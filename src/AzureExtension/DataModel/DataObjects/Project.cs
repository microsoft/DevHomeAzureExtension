// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.Core.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("Project")]
public class Project
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(Project)}"));

    private static readonly ILogger _log = _logger.Value;

    // This is the time between seeing a potential updated Project record and updating it.
    // This value / 2 is the average time between Project updating their Project data and having
    // it reflected in the datastore.
    private static readonly long _updateThreshold = TimeSpan.FromHours(4).Ticks;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    // Guid representation by ADO.
    public string InternalId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Key in the Organization table.
    public long OrganizationId { get; set; } = DataStore.NoForeignKey;

    // When this record was updated
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    public DateTime LastSyncAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    public Organization Organization => Organization.Get(DataStore, OrganizationId);

    // Connection URI for projects always uses the Guid instead of the name, since the Guid cannot
    // change for the project, but the name can change. This ensures any Uris we supply via this
    // method will be consistent across project renames.
    [Write(false)]
    [Computed]
    public Uri ConnectionUri => new($"{Organization.Get(DataStore, OrganizationId).ConnectionUri}{InternalId}/");

    public override string ToString() => Name;

    private static Project CreateFromTeamProject(TeamProject project, long organizationId)
    {
        return new Project
        {
            InternalId = project.Id.ToString(),
            Name = project.Name ?? string.Empty,
            Description = project.Description ?? string.Empty,
            OrganizationId = organizationId,
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    private static Project CreateFromTeamProject(TeamProjectReference project, long organizationId)
    {
        return new Project
        {
            InternalId = project.Id.ToString(),
            Name = project.Name ?? string.Empty,
            Description = project.Description ?? string.Empty,
            OrganizationId = organizationId,
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    public static Project AddOrUpdateProject(DataStore dataStore, Project project)
    {
        // Check for existing Project data.
        var existingProject = GetByInternalId(dataStore, project.InternalId);
        if (existingProject is not null)
        {
            // Many of the same Project records will be created on a sync, and to
            // avoid unnecessary updating and database operations for data that
            // is extremely unlikely to have changed in any significant way, we
            // will only update every UpdateThreshold amount of time.
            if ((project.TimeUpdated - existingProject.TimeUpdated) > _updateThreshold)
            {
                project.Id = existingProject.Id;
                dataStore.Connection!.Update(project);
                project.DataStore = dataStore;
                return project;
            }
            else
            {
                return existingProject;
            }
        }

        // No existing project, add it.
        project.Id = dataStore.Connection!.Insert(project);
        project.DataStore = dataStore;
        return project;
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static Project Get(DataStore? dataStore, long id)
    {
        if (dataStore == null)
        {
            return new Project();
        }

        var project = dataStore.Connection!.Get<Project>(id);
        if (project != null)
        {
            project.DataStore = dataStore;
        }

        return project ?? new Project();
    }

    public static Project? Get(DataStore? dataStore, string projectName, string organizationName)
    {
        if (dataStore == null)
        {
            return null;
        }

        // This is not guaranteed to only return one result, such as the case of multiple
        // organization records with different connections.
        var sql = @"SELECT * FROM Project AS P WHERE (P.Name = @Name OR P.InternalId = @Name) AND P.OrganizationId IN (SELECT Id FROM Organization WHERE Organization.Name = @Org)";
        var param = new
        {
            Name = projectName,
            Org = organizationName,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var project = dataStore.Connection!.QueryFirstOrDefault<Project>(sql, param, null);
        if (project is not null)
        {
            project.DataStore = dataStore;
        }

        return project;
    }

    // Internal id should be a GUID, which is by definition unique so is sufficient for lookup.
    public static Project? GetByInternalId(DataStore dataStore, string internalId)
    {
        var sql = @"SELECT * FROM Project WHERE InternalId = @InternalId;";
        var param = new
        {
            InternalId = internalId,
        };

        var project = dataStore.Connection!.QueryFirstOrDefault<Project>(sql, param, null);
        if (project != null)
        {
            project.DataStore = dataStore;
        }

        return project;
    }

    // Name requires an OrganizationId for lookup because name is not guaranteed unique among
    // projects. Internal Id is the best lookup.
    public static Project? Get(DataStore dataStore, string name, long organizationId)
    {
        var sql = @"SELECT * FROM Project WHERE Name = @Name AND OrganizationId = @OrganizationId;";
        var param = new
        {
            Name = name,
            OrganizationId = organizationId,
        };

        var project = dataStore.Connection!.QueryFirstOrDefault<Project>(sql, param, null);
        if (project != null)
        {
            project.DataStore = dataStore;
        }

        return project;
    }

    public static IEnumerable<Project> GetAllWithReference(DataStore dataStore)
    {
        var sql = @"SELECT * FROM Project AS P WHERE P.Id IN (SELECT ProjectId FROM ProjectReference)";
        _log.Verbose(DataStore.GetSqlLogMessage(sql));
        var projects = dataStore.Connection!.Query<Project>(sql, null, null) ?? [];
        foreach (var project in projects)
        {
            project.DataStore = dataStore;
        }

        return projects;
    }

    public static Project GetOrCreateByTeamProject(DataStore dataStore, TeamProject project, long organizationId)
    {
        var newProject = CreateFromTeamProject(project, organizationId);
        return AddOrUpdateProject(dataStore, newProject);
    }

    public static Project GetOrCreateByTeamProject(DataStore dataStore, TeamProjectReference project, long organizationId)
    {
        var newProject = CreateFromTeamProject(project, organizationId);
        return AddOrUpdateProject(dataStore, newProject);
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any Projects that have no matching Organization.
        var sql = @"DELETE FROM Project WHERE OrganizationId NOT IN (SELECT Id FROM Organization)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        _log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
