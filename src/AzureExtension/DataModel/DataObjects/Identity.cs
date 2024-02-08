// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Microsoft.VisualStudio.Services.WebApi;

namespace DevHomeAzureExtension.DataModel;

[Table("Identity")]
public class Identity
{
    // This is the time between seeing a potential updated Identity record and updating it.
    // This value / 2 is the average time between Identity updating their Identity data and having
    // it reflected in the datastore.
    private static readonly long UpdateThreshold = TimeSpan.FromHours(4).Ticks;

    [Key]
    [JsonIgnore]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    // Guid from Azure
    public string InternalId { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    [JsonIgnore]
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    [JsonIgnore]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    public string ToJson() => JsonSerializer.Serialize(this);

    public override string ToString() => Name;

    public static Identity? FromJson(DataStore dataStore, string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<Identity>(json);
            if (deserialized == null)
            {
                return null;
            }

            return GetByInternalId(dataStore, deserialized.InternalId);
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Failed to deserialize Json object into Identity: {json}", ex);
            return null;
        }
    }

    private static Identity CreateFromIdentityRef(IdentityRef identityRef)
    {
        // It is possible the IdentityRef object content is null but contains
        // a display name like "Closed". This is what is given to work items
        // that are closed and have no valid identity assigned.
        if (identityRef.Id == null)
        {
            return new Identity
            {
                // Since normal ids are GUIDs, we will treat a null identity as having
                // an InternalId equal to the displayName so these no-identity records
                // will be valid in our datastore. Since this is a string and the only
                // data in these identities, this should be safe and not collide with
                // real identity guids.
                InternalId = identityRef.DisplayName,
                Name = identityRef.DisplayName,
                Avatar = Links.GetLinkHref(identityRef.Links, "avatar"),
                TimeUpdated = DateTime.Now.ToDataStoreInteger(),
            };
        }

        return new Identity
        {
            InternalId = identityRef.Id,
            Name = identityRef.DisplayName,
            Avatar = Links.GetLinkHref(identityRef.Links, "avatar"),
            TimeUpdated = DateTime.Now.ToDataStoreInteger(),
        };
    }

    public static Identity AddOrUpdateIdentity(DataStore dataStore, Identity identity)
    {
        // Check for existing Identity data.
        var existingIdentity = GetByInternalId(dataStore, identity.InternalId);
        if (existingIdentity is not null)
        {
            // Many of the same Identity records will be created on a sync, and to
            // avoid unnecessary updating and database operations for data that
            // is extremely unlikely to have changed in any significant way, we
            // will only update every UpdateThreshold amount of time.
            if ((identity.TimeUpdated - existingIdentity.TimeUpdated) > UpdateThreshold)
            {
                identity.Id = existingIdentity.Id;
                dataStore.Connection!.Update(identity);
                return identity;
            }
            else
            {
                return existingIdentity;
            }
        }

        // No existing pull request, add it.
        identity.Id = dataStore.Connection!.Insert(identity);
        return identity;
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static Identity Get(DataStore? dataStore, long id)
    {
        if (dataStore == null)
        {
            return new Identity();
        }

        return dataStore.Connection!.Get<Identity>(id) ?? new Identity();
    }

    public static Identity? GetByInternalId(DataStore dataStore, string internalId)
    {
        var sql = @"SELECT * FROM Identity WHERE InternalId = @InternalId;";
        var param = new
        {
            InternalId = internalId,
        };

        return dataStore.Connection!.QueryFirstOrDefault<Identity>(sql, param, null);
    }

    public static Identity GetOrCreateIdentity(DataStore dataStore, IdentityRef? identityRef)
    {
#pragma warning disable CA1510 // Use ArgumentNullException throw helper ThrowIfNull does not tell compiler that the value is not null
        if (identityRef == null)
        {
            throw new ArgumentNullException(nameof(identityRef));
        }
#pragma warning restore CA1510 // Use ArgumentNullException throw helper

        var newIdentity = CreateFromIdentityRef(identityRef);
        return AddOrUpdateIdentity(dataStore, newIdentity);
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete queries older than the date listed.
        var sql = @"DELETE FROM Identity WHERE TimeUpdated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        Log.Logger()?.ReportDebug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        Log.Logger()?.ReportDebug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
