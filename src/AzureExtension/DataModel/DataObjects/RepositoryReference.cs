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

    public long PullRequestCount { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    public long Value => PullRequestCount * WeightPullRequest;

    private static RepositoryReference Create(long repositoryId, long pullRequestCount)
    {
        return new RepositoryReference
        {
            RepositoryId = repositoryId,
            PullRequestCount = pullRequestCount,
        };
    }

    public static RepositoryReference AddOrUpdate(DataStore dataStore, RepositoryReference repositoryReference)
    {
        var existing = Get(dataStore, repositoryReference.RepositoryId);
        if (existing is not null)
        {
            repositoryReference.Id = existing.Id;
            dataStore.Connection!.Update(repositoryReference);
            return repositoryReference;
        }

        repositoryReference.Id = dataStore.Connection!.Insert(repositoryReference);
        return repositoryReference;
    }

    public static RepositoryReference GetOrCreate(DataStore dataStore, long repositoryId, long pullRequestCount)
    {
        var repositoryReference = Create(repositoryId, pullRequestCount);
        return AddOrUpdate(dataStore, repositoryReference);
    }

    // Get by Repository row Id
    public static RepositoryReference? Get(DataStore dataStore, long repositoryId)
    {
        var sql = @"SELECT * FROM RepositoryReference WHERE RepositoryId = @RepositoryId";
        var param = new
        {
            RepositoryId = repositoryId,
        };

        return dataStore.Connection!.QueryFirstOrDefault<RepositoryReference>(sql, param, null);
    }

    // Get by Repository object
    public static RepositoryReference? Get(DataStore dataStore, Repository repository)
    {
        return Get(dataStore, repository.Id);
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any RepositoryReferences for repositories that do not exist.
        var sql = @"DELETE FROM RepositoryReference WHERE RepositoryId NOT IN (SELECT Id FROM Repository)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        Log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
