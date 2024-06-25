// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

// This table represents direct references to a repository, such as a pull request count
// or a widget, or clone being created for that repository. This is a reference that the
// repository has some significance to the user out of the potentially thousands of repositories
// to which they may have access.
[Table("ProjectReference")]
public class ProjectReference
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", $"DataModel/{nameof(ProjectReference)}"));

    private static readonly ILogger _log = _logger.Value;

    private static readonly long _weightPullRequest = 1;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public long DeveloperId { get; set; } = DataStore.NoForeignKey;

    public long PullRequestCount { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    public long Value => PullRequestCount * _weightPullRequest;

    [Write(false)]
    [Computed]
    public Identity Developer => Identity.Get(DataStore, DeveloperId);

    [Write(false)]
    private DataStore? DataStore { get; set; }

    private static ProjectReference Create(long projectId, long developerId, long pullRequestCount)
    {
        return new ProjectReference
        {
            ProjectId = projectId,
            DeveloperId = developerId,
            PullRequestCount = pullRequestCount,
        };
    }

    public static ProjectReference AddOrUpdate(DataStore dataStore, ProjectReference projectReference)
    {
        var existing = Get(dataStore, projectReference.ProjectId, projectReference.DeveloperId);
        if (existing is not null)
        {
            projectReference.Id = existing.Id;
            dataStore.Connection!.Update(projectReference);
            projectReference.DataStore = dataStore;
            return projectReference;
        }

        projectReference.Id = dataStore.Connection!.Insert(projectReference);
        projectReference.DataStore = dataStore;
        return projectReference;
    }

    public static ProjectReference GetOrCreate(DataStore dataStore, long projectId, long developerId, long pullRequestCount)
    {
        var projectReference = Create(projectId, developerId, pullRequestCount);
        return AddOrUpdate(dataStore, projectReference);
    }

    public static ProjectReference? Get(DataStore dataStore, long projectId, long developerId)
    {
        var sql = @"SELECT * FROM ProjectReference WHERE ProjectId = @ProjectId AND DeveloperId = @DeveloperId";
        var param = new
        {
            ProjectId = projectId,
            DeveloperId = developerId,
        };

        var projectReference = dataStore.Connection!.QueryFirstOrDefault<ProjectReference>(sql, param, null);
        if (projectReference != null)
        {
            projectReference.DataStore = dataStore;
        }

        return projectReference;
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any ProjectReferences for projects that do not exist.
        var sql = @"DELETE FROM ProjectReference WHERE (ProjectId NOT IN (SELECT Id FROM Project)) OR (DeveloperId NOT IN (SELECT Id FROM Identity))";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        _log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
