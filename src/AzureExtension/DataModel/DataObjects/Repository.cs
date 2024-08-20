// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("Repository")]
public class Repository
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(Repository)}"));

    private static readonly ILogger _log = _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // Name should be unique within a project scope.
    public string Name { get; set; } = string.Empty;

    // Guid representation by ADO.
    public string InternalId { get; set; } = string.Empty;

    // Key in the Project table.
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public long IsPrivate { get; set; } = DataStore.NoForeignKey;

    public string CloneUrl { get; set; } = string.Empty;

    // When this record was updated
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    public bool Private => IsPrivate != 0L;

    [Write(false)]
    [Computed]
    public AzureUri Clone => new(CloneUrl);

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public long ReferenceValue
    {
        get
        {
            if (DataStore is null)
            {
                return 0;
            }
            else
            {
                return RepositoryReference.GetRepositoryValue(DataStore, Id);
            }
        }
    }

    public override string ToString() => Name;

    private static Repository Create(GitRepository gitRepository, long projectId)
    {
        return new Repository
        {
            InternalId = gitRepository.Id.ToString(),
            Name = gitRepository.Name,
            CloneUrl = gitRepository.WebUrl,
            IsPrivate = gitRepository.ProjectReference.Visibility == Microsoft.TeamFoundation.Core.WebApi.ProjectVisibility.Private ? 1L : 0L,
            ProjectId = projectId,
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    public static Repository AddOrUpdate(DataStore dataStore, Repository repository)
    {
        // Check for existing Repository data.
        var existingRepository = GetByInternalId(dataStore, repository.InternalId);
        if (existingRepository is not null)
        {
            repository.Id = existingRepository.Id;
            dataStore.Connection!.Update(repository);
            repository.DataStore = dataStore;
            return repository;
        }

        // No existing repository, add it.
        repository.Id = dataStore.Connection!.Insert(repository);
        repository.DataStore = dataStore;
        return repository;
    }

    public static Repository GetOrCreate(DataStore dataStore, GitRepository gitRepository, long projectId)
    {
        // This will always update the repository record every time we see it, which means
        // UpdatedAt is effectively the last time we observed the Repository existing.
        var repository = Create(gitRepository, projectId);
        return AddOrUpdate(dataStore, repository);
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static Repository Get(DataStore? dataStore, long id)
    {
        if (dataStore == null)
        {
            return new Repository();
        }

        var repository = dataStore.Connection!.Get<Repository>(id);
        if (repository != null)
        {
            repository.DataStore = dataStore;
        }

        return repository ?? new Repository();
    }

    public static Repository? GetByName(DataStore? dataStore, long projectId, string name)
    {
        if (dataStore == null)
        {
            return null;
        }

        var sql = @"SELECT * FROM Repository WHERE ProjectId = @ProjectId AND Name = @Name;";
        var param = new
        {
            ProjectId = projectId,
            Name = name,
        };

        var repository = dataStore.Connection!.QueryFirstOrDefault<Repository>(sql, param, null);
        if (repository != null)
        {
            repository.DataStore = dataStore;
        }

        return repository;
    }

    public static IEnumerable<Repository> GetAll(DataStore dataStore)
    {
        var repositories = dataStore.Connection!.GetAll<Repository>() ?? [];
        foreach (var repository in repositories)
        {
            repository.DataStore = dataStore;
        }

        _log.Information("Getting all repositories.");
        return repositories;
    }

    // Internal id should be a GUID, which is by definition unique so is sufficient for lookup.
    public static Repository? GetByInternalId(DataStore dataStore, string internalId)
    {
        var sql = @"SELECT * FROM Repository WHERE InternalId = @InternalId;";
        var param = new
        {
            InternalId = internalId,
        };

        var repository = dataStore.Connection!.QueryFirstOrDefault<Repository>(sql, param, null);
        if (repository != null)
        {
            repository.DataStore = dataStore;
        }

        return repository;
    }

    public static Repository? Get(DataStore dataStore, GitRepository repository)
    {
        return GetByInternalId(dataStore, repository.Id.ToString());
    }

    public static Repository? Get(DataStore dataStore, long projectId, string repositoryName)
    {
        var sql = @"SELECT * FROM Repository WHERE ProjectId = @ProjectId AND Name = @RepositoryName;";
        var param = new
        {
            ProjectId = projectId,
            RepositoryName = repositoryName,
        };

        // In rare cases we might have a rename situation that results in more than one record.
        // This should be an extremely rare edge case that can be resolved in the next repository
        // cache update so we will assume a single record is correct.
        var repository = dataStore.Connection!.QueryFirstOrDefault<Repository>(sql, param, null);
        if (repository != null)
        {
            repository.DataStore = dataStore;
        }

        return repository;
    }

    public static IEnumerable<Repository> GetAllWithReference(DataStore dataStore)
    {
        var sql = @"SELECT * FROM Repository AS R WHERE R.Id IN (SELECT RepositoryId FROM RepositoryReference)";
        _log.Verbose(DataStore.GetSqlLogMessage(sql));
        var repositories = dataStore.Connection!.Query<Repository>(sql) ?? [];
        foreach (var repository in repositories)
        {
            repository.DataStore = dataStore;
        }

        return repositories;
    }

    public static void DeleteUnreferenced(DataStore dataStore)
    {
        // Delete any Repositories that have no matching Project.
        var sql = @"DELETE FROM Repository WHERE ProjectId NOT IN (SELECT Id FROM Project)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        _log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        command.ExecuteNonQuery();
    }
}
