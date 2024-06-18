﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Serilog;
using DevId = DevHomeAzureExtension.DeveloperId;

namespace DevHomeAzureExtension.DataModel;

[Table("PullRequests")]
public class PullRequests
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(PullRequests)}"));

    private static readonly ILogger Log = _log.Value;

    // This is the time between seeing a search and updating it's TimeUpdated.
    private static readonly long _updateThreshold = TimeSpan.FromMinutes(2).Ticks;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string RepositoryName { get; set; } = string.Empty;

    // Key in Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    // We need developer id because pullRequests results may be different depending on the user.
    public string DeveloperLogin { get; set; } = string.Empty;

    public string Results { get; set; } = string.Empty;

    public long ViewId { get; set; } = DataStore.NoForeignKey;

    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    public override string ToString() => DeveloperLogin + "/" + RepositoryName;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public PullRequestView View => (PullRequestView)ViewId;

    [Write(false)]
    [Computed]
    public DevId.DeveloperId? DeveloperId => DevId.DeveloperIdProvider.GetInstance().GetDeveloperIdFromAccountIdentifier(DeveloperLogin);

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    private static PullRequests Create(string repositoryName, long projectId, string developerLogin, PullRequestView view, string pullRequests)
    {
        return new PullRequests
        {
            RepositoryName = repositoryName,
            ProjectId = projectId,
            DeveloperLogin = developerLogin,
            Results = pullRequests,
            ViewId = (long)view,
            TimeUpdated = DateTime.Now.ToDataStoreInteger(),
        };
    }

    private static PullRequests AddOrUpdate(DataStore dataStore, PullRequests pullRequests)
    {
        var existing = Get(dataStore, pullRequests.ProjectId, pullRequests.RepositoryName, pullRequests.DeveloperLogin, pullRequests.View);
        if (existing is not null)
        {
            // Update threshold is in case there are many requests in a short period of time.
            if ((pullRequests.TimeUpdated - existing.TimeUpdated) > _updateThreshold)
            {
                pullRequests.Id = existing.Id;
                dataStore.Connection!.Update(pullRequests);
                return pullRequests;
            }
            else
            {
                return existing;
            }
        }

        // No existing search, add it.
        pullRequests.Id = dataStore.Connection!.Insert(pullRequests);
        return pullRequests;
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static PullRequests Get(DataStore dataStore, long id)
    {
        if (dataStore == null)
        {
            return new PullRequests();
        }

        var pullRequests = dataStore.Connection!.Get<PullRequests>(id);
        if (pullRequests != null)
        {
            pullRequests.DataStore = dataStore;
        }

        return pullRequests ?? new PullRequests();
    }

    public static PullRequests? Get(DataStore dataStore, long projectId, string repositoryName, string developerLogin, PullRequestView view)
    {
        var sql = @"SELECT * FROM PullRequests WHERE ProjectId = @ProjectId AND RepositoryName = @RepositoryName AND DeveloperLogin = @DeveloperLogin AND ViewId = @ViewId;";
        var param = new
        {
            ProjectId = projectId,
            RepositoryName = repositoryName,
            DeveloperLogin = developerLogin,
            ViewId = (long)view,
        };

        var pullRequests = dataStore.Connection!.QueryFirstOrDefault<PullRequests>(sql, param, null);
        if (pullRequests is not null)
        {
            pullRequests.DataStore = dataStore;
        }

        return pullRequests;
    }

    // DeveloperPullRequests is unique on developerId, repositoryName, project, and organization.
    public static PullRequests? Get(DataStore dataStore, string organizationName, string projectName, string repositoryName, string developerLogin, PullRequestView view)
    {
        // Since this also requires organization information and project is referenced by Id, we must first look up the project.
        var project = Project.Get(dataStore, projectName, organizationName);
        if (project == null)
        {
            return null;
        }

        return Get(dataStore, project.Id, repositoryName, developerLogin, view);
    }

    public static PullRequests GetOrCreate(DataStore dataStore, string repositoryName, long projectId, string developerId, PullRequestView view, string pullRequests)
    {
        var newDeveloperPullRequests = Create(repositoryName, projectId, developerId, view, pullRequests);
        return AddOrUpdate(dataStore, newDeveloperPullRequests);
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete queries older than the date listed.
        var sql = @"DELETE FROM PullRequests WHERE TimeUpdated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        Log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        Log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
