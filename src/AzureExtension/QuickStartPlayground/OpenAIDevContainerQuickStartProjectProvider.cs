// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.QuickStartPlayground;

public sealed partial class OpenAIDevContainerQuickStartProjectProvider : DevContainerQuickStartProjectProvider
{
    private static readonly Uri _privacyUri = new("https://openai.com/policies/privacy-policy");
    private static readonly Uri _termsUri = new("https://openai.com/policies/terms-of-use");
    internal const string LoginId = "OpenAI";

    public OpenAIDevContainerQuickStartProjectProvider(AzureOpenAIServiceFactory azureOpenAIServiceFactory, IInstalledAppsService installedAppsService)
        : base(
            azureOpenAIServiceFactory(OpenAIEndpoint.OpenAI),
            installedAppsService,
            Resources.GetResource(@"QuickStartProjectProviderUsingOpenAIDisplayName"),
            _privacyUri,
            _termsUri)
    {
    }

    public override QuickStartProjectAdaptiveCardResult CreateAdaptiveCardSessionForAIProviderInitialization() => AIProviderInitializationUIController.GetAdaptiveCard(AzureOpenAIService);

    public sealed partial class AIProviderInitializationUIController : IExtensionAdaptiveCardSession2
    {
        private readonly ILogger _log = Serilog.Log.ForContext("SourceContext", nameof(AIProviderInitializationUIController));

        public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

        private readonly IAzureOpenAIService _azureOpenAIService;

        public static QuickStartProjectAdaptiveCardResult GetAdaptiveCard(IAzureOpenAIService azureOpenAIService)
        {
            if (azureOpenAIService.AICredentialService.GetCredentials(LoginId, LoginId) is not null)
            {
                return null!;
            }

            return new QuickStartProjectAdaptiveCardResult(new AIProviderInitializationUIController(azureOpenAIService));
        }

        public AIProviderInitializationUIController(IAzureOpenAIService azureOpenAIService)
        {
            _azureOpenAIService = azureOpenAIService;
        }

        public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
        {
            return extensionUI.Update(GetTemplate(), string.Empty, string.Empty);
        }

        private static string GetTemplate()
        {
            var aiInitializationPage = @"
{
    ""type"": ""AdaptiveCard"",
    ""body"": [
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_Text1") + @"""
        },
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_Text2") + @""",
            ""wrap"": true
        },
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_Text3") + @"""
        },
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_Text4") + @""",
            ""wrap"": true
        },
        {
            ""type"": ""Input.Text"",
            ""placeholder"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_APIKey_Input") + @""",
            ""tooltip"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_APIKey_Input") + @""",
            ""isRequired"": true,
            ""maxLength"": 500,
            ""errorMessage"": """ + Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_APIKey_Input_ErrorMsg") + @""",
            ""style"": ""Password"",
            ""id"": ""Key""
        }
    ],
    ""actions"": [
        {
            ""type"": ""Action.Submit"",
            ""title"": """ + Resources.GetResource(@"Continue") + @""",
            ""style"": ""positive"",
            ""id"": ""submitAction""
        },
        {
            ""type"": ""Action.Execute"",
            ""title"": """ + Resources.GetResource(@"Cancel") + @""",
            ""associatedInputs"" : ""None"",
            ""id"": ""cancelAction""
        }
    ],
    ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
    ""version"": ""1.5"",
    ""verticalContentAlignment"": ""Top"",
    ""rtl"": false
}";
            return aiInitializationPage;
        }

        public unsafe IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
        {
            ProviderOperationResult result;
            var operationCanceled = false;

            _log.Information("OnAction: {action}", action);

            return Task.Run(() =>
            {
                try
                {
                    var adaptiveCardPayload = JsonSerializer.Deserialize(action, AIAdaptiveCardPayloadSourceGeneration.Default.AIAdaptiveCardActionPayload);
                    if (adaptiveCardPayload is not null)
                    {
                        if (adaptiveCardPayload.IsSubmitAction)
                        {
                            var inputPayload = JsonSerializer.Deserialize(inputs, AIAdaptiveCardPayloadSourceGeneration.Default.AIAdaptiveCardInputPayload);
                            if (inputPayload is not null && !string.IsNullOrEmpty(inputPayload.Key))
                            {
                                fixed (char* keyPtr = inputPayload.Key)
                                {
                                    SecureString secureString = new(keyPtr, inputPayload.Key.Length);
                                    _azureOpenAIService.AICredentialService.SaveCredentials(LoginId, LoginId, secureString);
                                }

                                _azureOpenAIService.InitializeAIClient();
                                result = new ProviderOperationResult(ProviderOperationStatus.Success, null, string.Empty, string.Empty);
                            }
                            else
                            {
                                _log.Error("API Key is null");
                                result = new ProviderOperationResult(ProviderOperationStatus.Failure, null, Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_InvalidAPIKey"), "API Key is null");
                            }
                        }
                        else if (adaptiveCardPayload.IsCancelAction)
                        {
                            // We will inform Dev Home using the stopped event that the user canceled
                            // but the result of OnAction is we successfully handled the user clicking cancel.
                            _log.Information("User canceled the operation.");
                            operationCanceled = true;
                            result = new ProviderOperationResult(ProviderOperationStatus.Success, null, string.Empty, string.Empty);
                        }
                        else
                        {
                            _log.Error("action Id is null");
                            result = new ProviderOperationResult(ProviderOperationStatus.Failure, null, Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_InvalidAPIKey"), "action Id is null");
                        }
                    }
                    else
                    {
                        _log.Error("adaptiveCardPayload is null");
                        result = new ProviderOperationResult(ProviderOperationStatus.Failure, null, Resources.GetResource(@"QuickstartPlayground_OpenAI_AdaptiveCard_InvalidAPIKey"), "adaptiveCardPayload is null");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Failed to process action: {ex}", ex);
                    result = new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, string.Empty);
                }

                // Inform Dev Home that we are done initializing AI provider.
                if (operationCanceled)
                {
                    _log.Information("Notifying Dev Home that AI provider initialization has been canceled");
                    var cancellationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, new OperationCanceledException(), string.Empty, string.Empty);
                    Stopped?.Invoke(this, new(cancellationResult, string.Empty));
                }
                else
                {
                    _log.Information("Notifying Dev Home that AI provider initialization is complete");
                    Stopped?.Invoke(this, new(result, string.Empty));
                }

                return result;
            }).AsAsyncOperation();
        }

        public void Dispose()
        {
        }

        internal sealed class AIAdaptiveCardActionPayload
        {
            public string? Id
            {
                get; set;
            }

            public bool IsSubmitAction => Id == "submitAction";

            public bool IsCancelAction => Id == "cancelAction";
        }

        internal sealed class AIAdaptiveCardInputPayload
        {
            public string? Key
            {
                get; set;
            }
        }

        [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
        [JsonSerializable(typeof(AIAdaptiveCardActionPayload))]
        [JsonSerializable(typeof(AIAdaptiveCardInputPayload))]
        internal sealed partial class AIAdaptiveCardPayloadSourceGeneration : JsonSerializerContext
        {
        }
    }
}
