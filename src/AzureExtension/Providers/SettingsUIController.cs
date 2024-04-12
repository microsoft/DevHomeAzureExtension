// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.Providers;

internal sealed class SettingsUIController : IExtensionAdaptiveCardSession
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", nameof(SettingsUIController)));

    private static readonly ILogger Log = _log.Value;

    private static readonly string _notificationsEnabledString = "NotificationsEnabled";

    private IExtensionAdaptiveCard? _settingsUI;
    private static readonly SettingsUITemplate _settingsUITemplate = new();

    public void Dispose()
    {
        Log.Debug($"Dispose");
        _settingsUI?.Update(null, null, null);
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        Log.Debug($"Initialize");
        _settingsUI = extensionUI;
        return _settingsUI.Update(_settingsUITemplate.GetSettingsUITemplate(), null, "SettingsPage");
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(async () =>
        {
            ProviderOperationResult operationResult;
            Log.Information($"OnAction() called with state:{_settingsUI?.State}");
            Log.Debug($"action: {action}");

            switch (_settingsUI?.State)
            {
                case "SettingsPage":
                    {
                        Log.Debug($"inputs: {inputs}");
                        var actionObject = JsonNode.Parse(action);
                        var verb = actionObject?["verb"]?.GetValue<string>() ?? string.Empty;
                        Log.Debug($"Verb: {verb}");
                        switch (verb)
                        {
                            case "ToggleNotifications":
                                var currentNotificationsEnabled = LocalSettings.ReadSettingAsync<string>(_notificationsEnabledString).Result ?? "true";
                                await LocalSettings.SaveSettingAsync(_notificationsEnabledString, currentNotificationsEnabled == "true" ? "false" : "true");
                                Log.Information($"Changed notification state to: {(currentNotificationsEnabled == "true" ? "false" : "true")}");
                                break;

                            case "UpdateData":
                                Log.Information($"Updating data for organizations...");
                                break;

                            case "OpenLogs":
                                Log.Information($"Opening Logs...");
                                FileHelper.OpenLogsLocation();
                                break;

                            default:
                                Log.Warning($"Unknown verb: {verb}");
                                break;
                        }

                        operationResult = _settingsUI.Update(_settingsUITemplate.GetSettingsUITemplate(), null, "SettingsPage");
                        break;
                    }

                default:
                    {
                        Log.Error($"Unexpected state:{_settingsUI?.State}");
                        operationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, null, "Something went wrong", $"Unexpected state:{_settingsUI?.State}");
                        break;
                    }
            }

            return operationResult;
        }).AsAsyncOperation();
    }

    // Adaptive Card Templates for SettingsUI.
    private sealed class SettingsUITemplate
    {
        internal string GetSettingsUITemplate()
        {
            var loader = new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), "AzureExtension/Resources");

            var notificationsEnabled = LocalSettings.ReadSettingAsync<string>(_notificationsEnabledString).Result ?? "true";
            var notificationsEnabledString = (notificationsEnabled == "true") ? loader.GetString("Settings_NotificationsEnabled") : loader.GetString("Settings_NotificationsDisabled");
            var updateAzureDataString = loader.GetString("Settings_UpdateData");

            var settingsUI = @"
{
    ""type"": ""AdaptiveCard"",
    ""body"": [
        {
            ""type"": ""ActionSet"",
            ""actions"": [
                {
                    ""type"": ""Action.Submit"",
                    ""title"": """ + $"{notificationsEnabledString}" + @""",
                    ""verb"": ""ToggleNotifications"",
                    ""associatedInputs"": ""auto""
                },
                {
                    ""type"": ""Action.Execute"",
                    ""title"": """ + $"{updateAzureDataString}" + @""",
                    ""verb"": ""UpdateData"",
                    ""tooltip"": """ + $"{updateAzureDataString}" + @"""
                },
                {
                    ""type"": ""Action.Execute"",
                    ""title"": """ + $"Open Logs" + @""",
                    ""verb"": ""OpenLogs"",
                    ""tooltip"": """ + $"Open Logs" + @"""
                }
            ]
        }
    ],
    ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
    ""version"": ""1.5"",
    ""minHeight"": ""200px""
}
";

            return settingsUI;
        }
    }
}
