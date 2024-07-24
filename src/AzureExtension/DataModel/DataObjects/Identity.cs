// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.VisualStudio.Services.Profile;
using Microsoft.VisualStudio.Services.Profile.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

// This represents an Azure DevOps Identity or IdentityRef.
[Table("Identity")]
public class Identity
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(Identity)}"));

    private static readonly ILogger _log = _logger.Value;

    // This is the time between seeing a potential updated Identity record and updating it.
    // This value / 2 is the average time between Identity updating their Identity data and having
    // it reflected in the datastore. We expect identity data to change very infrequently.
    // Since updating an identity involves re-fetching an avatar image, we want to do this
    // infrequently.
    private static readonly TimeSpan _updateThreshold = TimeSpan.FromDays(3);

    // Avatars may fail to download, in this case we will retry more frequently than the normal
    // update threshold since this can be intermittent.
    private static readonly TimeSpan _avatarRetryDelay = TimeSpan.FromHours(1);

    [Key]
    [JsonIgnore]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    // Guid from Azure
    public string InternalId { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    // The DeveloperLoginId associated with this identity, if one exists.
    public string? DeveloperLoginId { get; set; }

    [JsonIgnore]
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [Computed]
    [JsonIgnore]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    [JsonIgnore]
    public bool IsLoggedInDeveloper => !string.IsNullOrEmpty(DeveloperLoginId);

    [Write(false)]
    [Computed]
    public DeveloperId.DeveloperId? DeveloperId
    {
        get
        {
            if (!IsLoggedInDeveloper)
            {
                return null;
            }

            var devIdProvider = DeveloperIdProvider.GetInstance();
            return devIdProvider.GetDeveloperIdFromAccountIdentifier(DeveloperLoginId!);
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    public override string ToString() => Name;

    public static string GetAvatar(VssConnection connection, Guid identity)
    {
        try
        {
            var client = connection.GetClient<ProfileHttpClient>();
            var avatar = client.GetAvatarAsync(identity, AvatarSize.Small).Result;
            _log.Debug($"Avatar found: {avatar.Value.Length} bytes.");
            return Convert.ToBase64String(avatar.Value);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"Failed getting Avatar for {identity}.");
            return string.Empty;
        }
    }

    public static Identity? FromJson(DataStore dataStore, string json)
    {
        var log = Log.ForContext("SourceContext", nameof(Identity));
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
            _log.Error(ex, $"Failed to deserialize Json object into Identity: {json}");
            return null;
        }
    }

    private static Identity CreateFromIdentityRef(IdentityRef identityRef, VssConnection connection)
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
                Avatar = string.Empty,
                TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
            };
        }

        return new Identity
        {
            InternalId = identityRef.Id,
            Name = identityRef.DisplayName,
            Avatar = GetAvatar(connection, new Guid(identityRef.Id)),
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    private static Identity CreateFromIdentity(Microsoft.VisualStudio.Services.Identity.Identity identity, VssConnection connection)
    {
        return new Identity
        {
            InternalId = identity.Id.ToString(),
            Name = identity.DisplayName,
            Avatar = GetAvatar(connection, identity.Id),
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
    }

    public static Identity AddOrUpdateIdentity(DataStore dataStore, Identity identity, string? developerLoginId = null)
    {
        // Check for existing Identity data.
        var existingIdentity = GetByInternalId(dataStore, identity.InternalId);
        if (existingIdentity is not null)
        {
            identity.Id = existingIdentity.Id;

            // We presume not a developer unless it is explicitly set.
            if (!string.IsNullOrEmpty(developerLoginId))
            {
                identity.DeveloperLoginId = developerLoginId;
            }

            dataStore.Connection!.Update(identity);
            return identity;
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

    // Creation from an Azure IdentityRef object.
    public static Identity GetOrCreateIdentity(DataStore dataStore, IdentityRef? identityRef, VssConnection connection, string developerLoginId = "")
    {
        ArgumentNullException.ThrowIfNull(identityRef);

        Identity? existing;
        if (identityRef.Id == null)
        {
            existing = GetByInternalId(dataStore, identityRef.DisplayName);
        }
        else
        {
            existing = GetByInternalId(dataStore, identityRef.Id);
        }

        // Check for whether we need to update the record.
        // We don't want to create an identity object and download a new avatar unless it needs to
        // be updated. In the event of an empty avatar we will retry more frequently to update it,
        // but not every time.
        var isDeveloper = !string.IsNullOrEmpty(developerLoginId);
        if (existing is null || (isDeveloper && !existing.IsLoggedInDeveloper) || ((DateTime.UtcNow - existing.UpdatedAt) > _updateThreshold)
            || (string.IsNullOrEmpty(existing.Avatar) && ((DateTime.UtcNow - existing.UpdatedAt) > _avatarRetryDelay)))
        {
            var newIdentity = CreateFromIdentityRef(identityRef, connection);
            return AddOrUpdateIdentity(dataStore, newIdentity, developerLoginId);
        }

        return existing;
    }

    // Creation from an Azure Identity object.
    public static Identity GetOrCreateIdentity(DataStore dataStore, Microsoft.VisualStudio.Services.Identity.Identity? identity, VssConnection connection, string developerLoginId = "")
    {
        ArgumentNullException.ThrowIfNull(identity);

        Identity? existing;
        existing = GetByInternalId(dataStore, identity.Id.ToString());

        // Check for whether we need to update the record.
        // We don't want to create an identity object and download a new avatar unlesss it needs to
        // be updated. In the event of an empty avatar we will retry more frequently to update it,
        // but not every time.
        var isDeveloper = !string.IsNullOrEmpty(developerLoginId);
        if (existing is null || (isDeveloper && !existing.IsLoggedInDeveloper) || ((DateTime.UtcNow - existing.UpdatedAt) > _updateThreshold)
            || (string.IsNullOrEmpty(existing.Avatar) && ((DateTime.UtcNow - existing.UpdatedAt) > _avatarRetryDelay)))
        {
            var newIdentity = CreateFromIdentity(identity, connection);
            return AddOrUpdateIdentity(dataStore, newIdentity, developerLoginId);
        }

        return existing;
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete queries older than the date listed.
        var sql = @"DELETE FROM Identity WHERE TimeUpdated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        _log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
