// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("Repository")]
public class Repository
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(Repository)}"));

    private static readonly ILogger Log = _log.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

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
    public bool Private => IsPrivate != 0;

    [Write(false)]
    [Computed]
    public AzureUri Clone => new(CloneUrl);

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    public override string ToString() => Name;

    private static Repository Create(GitRepository gitRepository, long projectId)
    {
        return new Repository
        {
            InternalId = gitRepository.Id.ToString(),
            Name = gitRepository.Name,
            CloneUrl = gitRepository.WebUrl,
            IsPrivate = gitRepository.ProjectReference.Visibility == Microsoft.TeamFoundation.Core.WebApi.ProjectVisibility.Private ? 1 : 0,
            ProjectId = projectId,
            TimeUpdated = DateTime.Now.ToDataStoreInteger(),
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
}
