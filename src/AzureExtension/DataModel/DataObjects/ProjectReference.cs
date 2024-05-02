// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

// This table represents direct references to a repository, such as a pull request count
// or a widget, or clone being created for that repository. This is a reference that the
// repository has some significance to the user out of the potentially thousands of repositories
// to which they may have access.
[Table("ProjectReference")]
public class ProjectReference
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(ProjectReference)}"));

    private static readonly ILogger Log = _log.Value;

    private static readonly long WeightPullRequest = 1;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public long PullRequestCount { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    public long Value => PullRequestCount * WeightPullRequest;

    private static ProjectReference Create(long projectId, long pullRequestCount)
    {
        return new ProjectReference
        {
            ProjectId = projectId,
            PullRequestCount = pullRequestCount,
        };
    }

    public static ProjectReference AddOrUpdate(DataStore dataStore, ProjectReference projectReference)
    {
        var existing = Get(dataStore, projectReference.ProjectId);
        if (existing is not null)
        {
            projectReference.Id = existing.Id;
            dataStore.Connection!.Update(projectReference);
            return projectReference;
        }

        projectReference.Id = dataStore.Connection!.Insert(projectReference);
        return projectReference;
    }

    public static ProjectReference GetOrCreate(DataStore dataStore, long projectId, long pullRequestCount)
    {
        var projectReference = Create(projectId, pullRequestCount);
        return AddOrUpdate(dataStore, projectReference);
    }

    // Get by Repository row Id
    public static ProjectReference? Get(DataStore dataStore, long projectId)
    {
        var sql = @"SELECT * FROM ProjectReference WHERE ProjectId = @ProjectId";
        var param = new
        {
            ProjectId = projectId,
        };

        return dataStore.Connection!.QueryFirstOrDefault<ProjectReference>(sql, param, null);
    }

    // Get by Repository object
    public static ProjectReference? Get(DataStore dataStore, Project project)
    {
        return Get(dataStore, project.Id);
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any ProjectReferences for projects that do not exist.
        var sql = @"DELETE FROM ProjectReference WHERE ProjectId NOT IN (SELECT Id FROM Project)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        Log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
