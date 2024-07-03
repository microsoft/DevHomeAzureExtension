// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.Helpers;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("Organization")]
public class Organization
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", $"DataModel/{nameof(Organization)}"));

    private static readonly ILogger _log = _logger.Value;

    // This is the time between seeing a potential updated Organization record and updating it.
    // This value / 2 is the average time between Organization updating their Organization data and
    // having it reflected in the datastore.
    private static readonly long _updateThreshold = TimeSpan.FromHours(4).Ticks;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    // Connection in string form. An organization may have multiple valid connection strings.
    // Example: https://dev.azure.com/{organization} and https://{organization}.visualstudio.com
    // We will treat the connection string as the unique identifier of the organization.
    // This leaves open potential duplicate data, but not likely much.
    // For this extension to function currently it does not matter.
    public string Connection { get; set; } = string.Empty;

    // When this record was updated.
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    // When all records related to this one (i.e. projects) were completely updated.
    public long TimeLastSync { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Will be used in near-future changes.")]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    public DateTime LastSyncAt => TimeLastSync.ToDateTime();

    [Write(false)]
    [Computed]
    public Uri ConnectionUri => new(Connection);

    public override string ToString() => Name;

    public void SetSynced()
    {
        TimeLastSync = DateTime.UtcNow.ToDataStoreInteger();
        if (DataStore?.Connection is not null)
        {
            DataStore.Connection.Update(this);
        }
    }

    // Sets updated and sync time for all organization rows to 0, causing them to qualify for updating.
    public static void ClearAllSyncData(DataStore dataStore)
    {
        var sql = @"UPDATE Organization SET (TimeLastSync, TimeUpdated) = ($Time, $Time);";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", DataStore.NoForeignKey);
        _log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsUpdated = command.ExecuteNonQuery();
        _log.Debug($"Updated {rowsUpdated} rows.");
    }

    private static Organization Create(Uri connection)
    {
        var azureUri = new AzureUri(connection);
        if (!azureUri.IsValid)
        {
            throw new ArgumentException("Connection Uri is not valid");
        }

        return new Organization
        {
            Name = azureUri.Organization,
            Connection = azureUri.Connection.ToString(),
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
            TimeLastSync = DateTime.MinValue.ToDataStoreInteger(),
        };
    }

    public static Organization AddOrUpdateOrganization(DataStore dataStore, Organization organization)
    {
        // Check for existing Organization data.
        var existingOrganization = Get(dataStore, organization.Connection);
        if (existingOrganization is not null)
        {
            // Many of the same Organization records will be created on a sync, and to
            // avoid unnecessary updating and database operations for data that
            // is extremely unlikely to have changed in any significant way, we
            // will only update every UpdateThreshold amount of time.
            if ((organization.TimeUpdated - existingOrganization.TimeUpdated) > _updateThreshold)
            {
                organization.Id = existingOrganization.Id;
                dataStore.Connection!.Update(organization);
                organization.DataStore = dataStore;
                return organization;
            }
            else
            {
                return existingOrganization;
            }
        }

        // No existing pull request, add it.
        organization.Id = dataStore.Connection!.Insert(organization);
        organization.DataStore = dataStore;
        return organization;
    }

    // Direct Get always returns a non-null object for reference in other objects.
    public static Organization Get(DataStore? dataStore, long id)
    {
        if (dataStore == null)
        {
            return new Organization();
        }

        var organization = dataStore.Connection!.Get<Organization>(id);
        if (organization != null)
        {
            organization.DataStore = dataStore;
        }

        return organization ?? new Organization();
    }

    public static Organization? Get(DataStore dataStore, string connection)
    {
        // Ensure trailing slash on connection
        connection = connection.Trim('/') + '/';

        var sql = @"SELECT * FROM Organization WHERE Connection = @Connection;";
        var param = new
        {
            Connection = connection,
        };

        var organization = dataStore.Connection!.QueryFirstOrDefault<Organization>(sql, param, null);
        if (organization != null)
        {
            organization.DataStore = dataStore;
        }

        return organization;
    }

    public static Organization GetOrCreate(DataStore dataStore, Uri connection)
    {
        var newOrganization = Create(connection);
        return AddOrUpdateOrganization(dataStore, newOrganization);
    }
}
