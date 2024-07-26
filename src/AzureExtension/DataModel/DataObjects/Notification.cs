// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

/// <summary>
/// Represents data for rendering a notification to the user.
/// </summary>
/// <remarks>
/// Notifications are sent to the user as Windows notifications, but may have specific filtering,
/// such as prioritizing certain repositories, or only showing a notification for a current
/// developer. The contents of the notifications table is a representation of potential
/// notifications, it does not necessarily mean the notification will be shown to the user. It is
/// the set of things we believe may be notification-worthy and ultimately user settings and context
/// will determine when and if the notification gets shown.
/// </remarks>
[Table("Notification")]
public class Notification
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(Notification)}"));

    private static ILogger Log => _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public long TypeId { get; set; } = DataStore.NoForeignKey;

    // Key in Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    // Key in Repository table
    public long RepositoryId { get; set; } = DataStore.NoForeignKey;

    public string Title { get; set; } = string.Empty;

    public string Identifier { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;

    public long ToastState { get; set; } = DataStore.NoForeignKey;

    public long TimeCreated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public DateTime CreatedAt => TimeCreated.ToDateTime();

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public Repository Repository => Repository.Get(DataStore, RepositoryId);

    [Write(false)]
    [Computed]
    public NotificationType Type => (NotificationType)TypeId;

    [Write(false)]
    [Computed]
    public bool Toasted
    {
        get => ToastState != 0;
        set
        {
            ToastState = value ? 1 : 0;
            if (DataStore is not null)
            {
                try
                {
                    DataStore.Connection!.Update(this);
                }
                catch (Exception ex)
                {
                    // Catch errors so we do not throw for something like this. The local ToastState
                    // will still be set even if the datastore update fails. This could result in a
                    // toast later being shown twice, however, so report it as an error.
                    Log.Error(ex, "Failed setting Notification ToastState for Notification Id = {Id}");
                }
            }
        }
    }

    public override string ToString() => $"[{Type}][{Project}] {Title}";

    /// <summary>
    /// Shows a toast formatted based on this notification's NotificationType.
    /// </summary>
    /// <returns>True if a toast was shown.</returns>
    public bool ShowToast()
    {
        if (Toasted)
        {
            return false;
        }
        else if (LocalSettings.ReadSettingAsync<string>("NotificationsEnabled").Result == "false")
        {
            Toasted = true;
            return false;
        }

        return Type switch
        {
            NotificationType.PullRequestRejected => ShowPullRequestRejectedToast(),
            NotificationType.PullRequestApproved => ShowPullRequestApprovedToast(),
            _ => false,
        };
    }

    private bool ShowPullRequestRejectedToast()
    {
        try
        {
            Log.Information($"Showing Notification for {this}");
            var nb = new AppNotificationBuilder();
            nb.SetDuration(AppNotificationDuration.Long);
            nb.AddArgument("htmlurl", HtmlUrl);
            nb.AddText($"❌ {Resources.GetResource("Notifications_Toast_PullRequestRejected/Title", Log)} - {Description}");
            nb.AddText($"#{Identifier} - {Repository.Name}", new AppNotificationTextProperties().SetMaxLines(1));
            nb.AddText(Title);
            nb.AddButton(new AppNotificationButton(Resources.GetResource("Notifications_Toast_Button/Dismiss", Log)).AddArgument("action", "dismiss"));
            AppNotificationManager.Default.Show(nb.BuildNotification());

            Toasted = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed creating the Notification for {this}");
            return false;
        }

        return true;
    }

    private bool ShowPullRequestApprovedToast()
    {
        try
        {
            Log.Information($"Showing Notification for {this}");
            var nb = new AppNotificationBuilder();
            nb.SetDuration(AppNotificationDuration.Long);
            nb.AddArgument("htmlurl", HtmlUrl);
            nb.AddText($"✅ {Resources.GetResource("Notifications_Toast_PullRequestApproved/Title", Log)}");
            nb.AddText($"#{Identifier} - {Repository.Name}", new AppNotificationTextProperties().SetMaxLines(1));
            nb.AddText(Title);
            nb.AddButton(new AppNotificationButton(Resources.GetResource("Notifications_Toast_Button/Dismiss", Log)).AddArgument("action", "dismiss"));
            AppNotificationManager.Default.Show(nb.BuildNotification());

            Toasted = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed creating the Notification for {this}");
            return false;
        }

        return true;
    }

    public static Notification Create(DataStore dataStore, PullRequestPolicyStatus status, NotificationType type)
    {
        var pullRequestNotification = new Notification
        {
            TypeId = (long)type,
            ProjectId = status.ProjectId,
            RepositoryId = status.RepositoryId,
            Title = status.Title,
            Description = status.PolicyStatusReason,
            Identifier = status.PullRequestId.ToStringInvariant(),
            Result = status.PolicyStatus.ToString(),
            HtmlUrl = status.HtmlUrl,
            ToastState = 0,
            TimeCreated = DateTime.Now.ToDataStoreInteger(),
        };

        Add(dataStore, pullRequestNotification);
        SetOlderNotificationsToasted(dataStore, pullRequestNotification);
        return pullRequestNotification;
    }

    public static Notification Add(DataStore dataStore, Notification notification)
    {
        notification.Id = dataStore.Connection!.Insert(notification);
        notification.DataStore = dataStore;
        return notification;
    }

    public static IEnumerable<Notification> Get(DataStore dataStore, DateTime? since = null, bool includeToasted = false)
    {
        since ??= DateTime.MinValue;
        var sql = @"SELECT * FROM Notification WHERE TimeCreated > @Time AND ToastState <= @ToastedCount ORDER BY TimeCreated DESC";
        var param = new
        {
            // Cast to non-nullable type since we ensure it is not null above.
            Time = ((DateTime)since).ToDataStoreInteger(),
            ToastedCount = includeToasted ? 1 : 0,
        };

        Log.Verbose(DataStore.GetSqlLogMessage(sql, param));
        var notifications = dataStore.Connection!.Query<Notification>(sql, param, null) ?? [];
        foreach (var notification in notifications)
        {
            notification.DataStore = dataStore;
        }

        return notifications;
    }

    public static void SetOlderNotificationsToasted(DataStore dataStore, Notification notification)
    {
        // Get all untoasted notifications for the same type, project, repository, and identifier that are older
        // than the specified notification.
        var sql = @"SELECT * FROM Notification WHERE TypeId = @TypeId AND RepositoryId = @RepositoryId AND Identifier = @Identifier AND ProjectId = @ProjectId AND TimeCreated < @TimeCreated AND ToastState = 0";
        var param = new
        {
            notification.TypeId,
            notification.RepositoryId,
            notification.Identifier,
            notification.ProjectId,
            notification.TimeCreated,
        };

        Log.Verbose(DataStore.GetSqlLogMessage(sql, param));
        var outDatedNotifications = dataStore.Connection!.Query<Notification>(sql, param, null) ?? [];
        foreach (var olderNotification in outDatedNotifications)
        {
            olderNotification.DataStore = dataStore;
            olderNotification.Toasted = true;
            Log.Information($"Found older notification for {olderNotification.Identifier} with result {olderNotification.Result}, marking toasted.");
        }
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete notifications older than the date listed.
        var sql = @"DELETE FROM Notification WHERE TimeCreated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        Log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        Log.Verbose(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
