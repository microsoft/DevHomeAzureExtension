// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.Providers;

internal sealed class SettingsUIController() : IExtensionAdaptiveCardSession
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(SettingsUIController)));

    private static readonly ILogger _log = _logger.Value;

    private static readonly string _notificationsEnabledString = "NotificationsEnabled";

    private string? _template;

    private IExtensionAdaptiveCard? _settingsUI;

    public void Dispose()
    {
        _log.Debug($"Dispose");
        _settingsUI?.Update(null, null, null);
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _log.Debug($"Initialize");
        CacheManager.GetInstance().OnUpdate += HandleCacheUpdate;
        _settingsUI = extensionUI;
        return UpdateCard();
    }

    private void HandleCacheUpdate(object? source, CacheManagerUpdateEventArgs e)
    {
        _log.Debug("Cache was updated, updating settings UI.");
        UpdateCard();
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(async () =>
        {
            try
            {
                _log.Information($"OnAction() called with {action}");
                _log.Debug($"inputs: {inputs}");
                var actionObject = JsonNode.Parse(action);
                var verb = actionObject?["verb"]?.GetValue<string>() ?? string.Empty;
                _log.Debug($"Verb: {verb}");
                switch (verb)
                {
                    case "ToggleNotifications":
                        var currentNotificationsEnabled = LocalSettings.ReadSettingAsync<string>(_notificationsEnabledString).Result ?? "true";
                        await LocalSettings.SaveSettingAsync(_notificationsEnabledString, currentNotificationsEnabled == "true" ? "false" : "true");
                        _log.Information($"Changed notification state to: {(currentNotificationsEnabled == "true" ? "false" : "true")}");
                        break;

                    case "UpdateData":
                        _log.Information($"Refreshing data for organizations.");
                        _ = CacheManager.GetInstance().Refresh();
                        break;

                    case "OpenLogs":
                        FileHelper.OpenLogsLocation();
                        break;

                    default:
                        _log.Warning($"Unknown verb: {verb}");
                        break;
                }

                return UpdateCard();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unexpected failure handling settings action.");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
            }
        }).AsAsyncOperation();
    }

    private ProviderOperationResult UpdateCard()
    {
        try
        {
            var notificationsEnabled = LocalSettings.ReadSettingAsync<string>(_notificationsEnabledString).Result ?? "true";
            var notificationsEnabledString = (notificationsEnabled == "true") ? Resources.GetResource("Settings_NotificationsEnabled", _log) : Resources.GetResource("Settings_NotificationsDisabled", _log);

            var lastUpdated = CacheManager.GetInstance().LastUpdated;
            var lastUpdatedString = $"Last updated: {lastUpdated.ToString(CultureInfo.InvariantCulture)}";
            if (lastUpdated == DateTime.MinValue)
            {
                lastUpdatedString = "Last updated: never";
            }

            var updateAzureDataString = Resources.GetResource("Settings_UpdateData", _log);
            if (CacheManager.GetInstance().UpdateInProgress)
            {
                updateAzureDataString = "Update in progress";
            }

            var settingsCardData = new SettingsCardData
            {
                NotificationsEnabled = notificationsEnabledString,
                CacheLastUpdated = lastUpdatedString,
                UpdateAzureData = updateAzureDataString,
            };

            return _settingsUI!.Update(
                GetTemplate(),
                JsonSerializer.Serialize(settingsCardData, SettingsCardSerializerContext.Default.SettingsCardData),
                "SettingsPage");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to update settings card");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
        }
    }

    private string GetTemplate()
    {
        if (_template is not null)
        {
            _log.Debug("Using cached template.");
            return _template;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, @"Providers\SettingsCardTemplate.json");
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
            template = Resources.ReplaceIdentifiers(template, GetSettingsCardResourceIdentifiers(), _log);
            _log.Debug($"Caching template");
            _template = template;
            return _template;
        }
        catch (Exception e)
        {
            _log.Error(e, "Error getting template.");
            return string.Empty;
        }
    }

    private static string[] GetSettingsCardResourceIdentifiers()
    {
        return
        [
            "Settings_ViewLogs",
        ];
    }
}
