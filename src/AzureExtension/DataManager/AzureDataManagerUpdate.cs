// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using Microsoft.TeamFoundation.Policy.WebApi;
using Serilog;

namespace DevHomeAzureExtension;

public partial class AzureDataManager
{
    // This is how frequently the DataStore update occurs.
    private static readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(5);
    private static DateTime _lastUpdateTime = DateTime.MinValue;

    public static async Task Update()
    {
        // Only update per the update interval.
        // This is intended to be dynamic in the future.
        if (DateTime.UtcNow - _lastUpdateTime < _updateInterval)
        {
            return;
        }

        try
        {
            await UpdateDeveloperPullRequests();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update failed unexpectedly.");
        }

        _lastUpdateTime = DateTime.UtcNow;
    }

    public static async Task UpdateDeveloperPullRequests()
    {
        var log = Log.ForContext("SourceContext", $"UpdateDeveloperPullRequests");
        log.Debug($"Executing UpdateDeveloperPullRequests");

        var cacheManager = CacheManager.GetInstance();
        if (cacheManager.UpdateInProgress)
        {
            log.Information("Cache is being updated, skipping Developer Pull Request Update");
            return;
        }

        var identifier = Guid.NewGuid();
        using var dataManager = CreateInstance(identifier.ToString()) ?? throw new DataStoreInaccessibleException();
        await dataManager.UpdatePullRequestsForLoggedInDeveloperIdsAsync(null, identifier);

        // Show any new notifications that were created from the pull request update.
        var notifications = dataManager.GetNotifications();
        foreach (var notification in notifications)
        {
            // Show notifications for failed checkruns for Developer users.
            if (notification.Type == NotificationType.PullRequestRejected || notification.Type == NotificationType.PullRequestApproved)
            {
                notification.ShowToast();
            }
        }
    }
}
