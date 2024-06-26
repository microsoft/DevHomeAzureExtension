// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.QuickStartPlayground;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.Providers;

internal sealed partial class SettingsUIController : IExtensionAdaptiveCardSession
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", nameof(SettingsUIController)));

    private static readonly ILogger Log = _log.Value;

    private readonly IAICredentialService _aiCredentialService;

    private IExtensionAdaptiveCard? _extensionUI;

    public SettingsUIController(IAICredentialService aiCredentialService)
    {
        _aiCredentialService = aiCredentialService;
        _extensionUI = null;
    }

    public void Dispose()
    {
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _extensionUI = extensionUI;
        return UpdateCard();
    }

    private ProviderOperationResult UpdateCard()
    {
        try
        {
            var hasOpenAIKey = _aiCredentialService.GetCredentials(OpenAIDevContainerQuickStartProjectProvider.LoginId, OpenAIDevContainerQuickStartProjectProvider.LoginId) is not null;
            SettingsAdaptiveCardInputPayload settingsAdaptiveCardInputPayload = new()
            {
                HasOpenAIKey = hasOpenAIKey,
            };

            return _extensionUI!.Update(
                GetTemplate(),
                JsonSerializer.Serialize(settingsAdaptiveCardInputPayload, SettingsAdaptiveCardPayloadSourceGeneration.Default.SettingsAdaptiveCardInputPayload),
                string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update settings card");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
        }
    }

    private static string GetTemplate()
    {
        var settingsUI = @"
{
    ""type"": ""AdaptiveCard"",
    ""body"": [
        {
            ""type"": ""ActionSet"",
            ""actions"": [
                {
                    ""type"": ""Action.Execute"",
                    ""title"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_ClearKey") + @""",
                    ""id"": ""clearOpenAIKey"",
                    ""isEnabled"": ""${HasOpenAIKey}""
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

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(() =>
        {
            try
            {
                var adaptiveCardPayload = JsonSerializer.Deserialize(action, SettingsAdaptiveCardPayloadSourceGeneration.Default.SettingsActionAdaptiveCardActionPayload);
                if (adaptiveCardPayload is not null)
                {
                    if (adaptiveCardPayload.IsClearOpenAIKeyAction)
                    {
                        Log.Information("Clearing OpenAI key");
                        _aiCredentialService.RemoveCredentials(OpenAIDevContainerQuickStartProjectProvider.LoginId, OpenAIDevContainerQuickStartProjectProvider.LoginId);
                        return UpdateCard();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear OpenAI key");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
            }

            return new ProviderOperationResult(ProviderOperationStatus.Failure, null, string.Empty, "Unexpected state");
        }).AsAsyncOperation();
    }

    internal sealed class SettingsActionAdaptiveCardActionPayload
    {
        public string? Id
        {
            get; set;
        }

        public bool IsClearOpenAIKeyAction => Id == "clearOpenAIKey";
    }

    internal sealed class SettingsAdaptiveCardInputPayload
    {
        public bool HasOpenAIKey { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(SettingsActionAdaptiveCardActionPayload))]
    [JsonSerializable(typeof(SettingsAdaptiveCardInputPayload))]
    internal sealed partial class SettingsAdaptiveCardPayloadSourceGeneration : JsonSerializerContext
    {
    }
}
