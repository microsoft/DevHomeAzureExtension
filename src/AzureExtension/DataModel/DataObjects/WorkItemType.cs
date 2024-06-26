// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Serilog;

// This is to resolve ambiguity between DataModel WorkItemType and
// TeamFoundation WorkItemType. We'll Rename the TeamFoundation one.
using TeamWorkItemType = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItemType;

namespace DevHomeAzureExtension.DataModel;

[Table("WorkItemType")]
public class WorkItemType
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(WorkItemType)}"));

    private static readonly ILogger _log = _logger.Value;

    // This is the time between seeing a potential updated WorkItemType record and updating it.
    // This value / 2 is the average time between WorkItemType updating their WorkItemType data
    // and having it reflected in the datastore.
    private static readonly long _updateThreshold = TimeSpan.FromHours(4).Ticks;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    // WorkItemType table reference
    [JsonIgnore]
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public string Icon { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    [JsonIgnore]
    public string Description { get; set; } = string.Empty;

    [JsonIgnore]
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    [JsonIgnore]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    public string ToJson() => JsonSerializer.Serialize(this);

    [Write(false)]
    [JsonIgnore]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    [JsonIgnore]
    public Project Project => Project.Get(DataStore, ProjectId);

    public override string ToString() => Name;

    public static WorkItemType? FromJson(DataStore dataStore, string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<WorkItemType>(json);
            if (deserialized == null)
            {
                return null;
            }

            return Get(dataStore, deserialized.Id);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Failed to deserialize Json object into WorkItemType: {json}");
            return null;
        }
    }

    private static WorkItemType CreateFromTeamWorkItemType(TeamWorkItemType workItemType, long projectId)
    {
        return new WorkItemType
        {
            Name = workItemType.Name ?? string.Empty,
            Description = workItemType.Description ?? string.Empty,
            Icon = workItemType.Icon.Url.ToString() ?? string.Empty,
            Color = workItemType.Color ?? string.Empty,
            ProjectId = projectId,
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    public static WorkItemType AddOrUpdateWorkItemType(DataStore dataStore, WorkItemType workItemType)
    {
        // Check for existing WorkItemType data.
        var existingWorkItemType = Get(dataStore, workItemType.Name, workItemType.ProjectId);
        if (existingWorkItemType is not null)
        {
            // Many of the same WorkItemType records will be created on a sync, and to
            // avoid unnecessary updating and database operations for data that
            // is extremely unlikely to have changed in any significant way, we
            // will only update every UpdateThreshold amount of time.
            if ((workItemType.TimeUpdated - existingWorkItemType.TimeUpdated) > _updateThreshold)
            {
                workItemType.Id = existingWorkItemType.Id;
                dataStore.Connection!.Update(workItemType);
                workItemType.DataStore = dataStore;
                return workItemType;
            }
            else
            {
                return existingWorkItemType;
            }
        }

        // No existing pull request, add it.
        workItemType.Id = dataStore.Connection!.Insert(workItemType);
        workItemType.DataStore = dataStore;
        return workItemType;
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static WorkItemType Get(DataStore? dataStore, long id)
    {
        if (dataStore == null)
        {
            return new WorkItemType();
        }

        var workItemType = dataStore.Connection!.Get<WorkItemType>(id);
        if (workItemType != null)
        {
            workItemType.DataStore = dataStore;
        }

        return workItemType ?? new WorkItemType();
    }

    // WorkItemTypes do not have a unique identifier and are looked up on the server by name
    // and project name/guid. We will treat this as a unique constraint for identification.
    public static WorkItemType? Get(DataStore dataStore, string name, long projectId)
    {
        if (dataStore == null)
        {
            return null;
        }

        var sql = @"SELECT * FROM WorkItemType WHERE Name = @Name AND ProjectId = @ProjectId;";
        var param = new
        {
            Name = name,
            ProjectId = projectId,
        };

        var workItemType = dataStore.Connection!.QueryFirstOrDefault<WorkItemType>(sql, param, null);
        if (workItemType != null)
        {
            workItemType.DataStore = dataStore;
        }

        return workItemType;
    }

    public static WorkItemType GetOrCreateByTeamWorkItemType(DataStore dataStore, TeamWorkItemType workItemType, long projectId)
    {
        var newWorkItemType = CreateFromTeamWorkItemType(workItemType, projectId);
        return AddOrUpdateWorkItemType(dataStore, newWorkItemType);
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete queries older than the date listed.
        var sql = @"DELETE FROM WorkItemType WHERE TimeUpdated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        _log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
