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
[Table("RepositoryReference")]
public class RepositoryReference
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(RepositoryReference)}"));

    private static readonly ILogger Log = _log.Value;

    private static readonly long WeightPullRequest = 1;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // Repository table
    public long RepositoryId { get; set; } = DataStore.NoForeignKey;

    public long DeveloperId { get; set; } = DataStore.NoForeignKey;

    public long PullRequestCount { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    public long Value => PullRequestCount * WeightPullRequest;

    [Write(false)]
    [Computed]
    public Identity Developer => Identity.Get(DataStore, DeveloperId);

    [Write(false)]
    private DataStore? DataStore { get; set; }

    public static long GetRepositoryValue(DataStore dataStore, long repositoryId)
    {
        // TODO: Sum up values of all references to this repository across users.
        return 0;
    }

    private static RepositoryReference Create(long repositoryId, long developerId, long pullRequestCount)
    {
        return new RepositoryReference
        {
            RepositoryId = repositoryId,
            DeveloperId = developerId,
            PullRequestCount = pullRequestCount,
        };
    }

    public static RepositoryReference AddOrUpdate(DataStore dataStore, RepositoryReference repositoryReference)
    {
        var existing = Get(dataStore, repositoryReference.RepositoryId, repositoryReference.DeveloperId);
        if (existing is not null)
        {
            repositoryReference.Id = existing.Id;
            dataStore.Connection!.Update(repositoryReference);
            repositoryReference.DataStore = dataStore;
            return repositoryReference;
        }

        repositoryReference.Id = dataStore.Connection!.Insert(repositoryReference);
        repositoryReference.DataStore = dataStore;
        return repositoryReference;
    }

    public static RepositoryReference GetOrCreate(DataStore dataStore, long repositoryId, long developerId, long pullRequestCount)
    {
        var repositoryReference = Create(repositoryId, developerId, pullRequestCount);
        return AddOrUpdate(dataStore, repositoryReference);
    }

    // Get by Repository row Id
    public static RepositoryReference? Get(DataStore dataStore, long repositoryId, long developerId)
    {
        var sql = @"SELECT * FROM RepositoryReference WHERE RepositoryId = @RepositoryId AND DeveloperId = @DeveloperId";
        var param = new
        {
            RepositoryId = repositoryId,
            DeveloperId = developerId,
        };

        var repositoryReference = dataStore.Connection!.QueryFirstOrDefault<RepositoryReference>(sql, param, null);
        if (repositoryReference != null)
        {
            repositoryReference.DataStore = dataStore;
        }

        return repositoryReference;
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any RepositoryReferences for repositories that do not exist.
        var sql = @"DELETE FROM RepositoryReference WHERE (RepositoryId NOT IN (SELECT Id FROM Repository)) OR (DeveloperId NOT IN (SELECT Id FROM Identity))";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        Log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
