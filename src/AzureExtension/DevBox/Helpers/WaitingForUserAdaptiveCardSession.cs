// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.DevBox.Helpers;

public class WaitingForUserAdaptiveCardSession : IExtensionAdaptiveCardSession2, IDisposable
{
    private readonly ManualResetEvent _resumeEvent;

    private readonly ManualResetEvent _launchEvent;

    private IExtensionAdaptiveCard? _extensionAdaptiveCard;

    private string? _template;

    private const string WaitingForUserSessionState = "WaitingForUserSession";

    private const string LaunchAction = "launchAction";

    private const string ResumeAction = "resumeAction";

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(WaitingForUserAdaptiveCardSession));

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public WaitingForUserAdaptiveCardSession(ManualResetEvent resumeEvent, ManualResetEvent launchEvent)
    {
        _resumeEvent = resumeEvent;
        _launchEvent = launchEvent;
    }

    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs> Stopped = (s, e) => { };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        var dataJson = new JsonObject
        {
            { "title", Resources.GetResource("DevBox_AdaptiveCard_Title") },
            { "description", Resources.GetResource("DevBox_AdaptiveCard_Description") },
            { "icon", ConvertIconToDataString("Caution.png") },
            { "loginRequiredText", Resources.GetResource("DevBox_AdaptiveCard_Text") },
            { "loginRequiredDescriptionText", Resources.GetResource("DevBox_AdaptiveCard_InnerDescription") },
            { "LaunchText", Resources.GetResource("DevBox_AdaptiveCard_LaunchText") },
            { "ResumeText", Resources.GetResource("DevBox_AdaptiveCard_ResumeText") },
        };

        _extensionAdaptiveCard = extensionUI;
        return _extensionAdaptiveCard.Update(
            LoadTemplate(),
            dataJson.ToJsonString(),
            "WaitingForUserSession");
    }

    private string LoadTemplate()
    {
        if (string.IsNullOrEmpty(_template))
        {
            var path = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", "WaitingForUserSessionTemplate.json");
            _template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
        }

        return _template;
    }

    private static string ConvertIconToDataString(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", fileName);
        var imageData = Convert.ToBase64String(File.ReadAllBytes(path.ToString()));
        return imageData;
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(() =>
        {
            try
            {
                var state = _extensionAdaptiveCard?.State;
                ProviderOperationResult operationResult;
                var data = JsonSerializer.Deserialize<AdaptiveCardJSONToCSClass>(action, _serializerOptions);
                if (state != null && state.Equals(WaitingForUserSessionState, StringComparison.OrdinalIgnoreCase) && data != null)
                {
                    switch (data.Id)
                    {
                        case LaunchAction:
                            operationResult = new ProviderOperationResult(ProviderOperationStatus.Success, null, null, null);
                            _launchEvent.Set();
                            _resumeEvent.Set();
                            break;
                        case ResumeAction:
                            operationResult = new ProviderOperationResult(ProviderOperationStatus.Success, null, null, null);
                            _resumeEvent.Set();
                            break;
                        default:
                            throw new InvalidOperationException($"Unexpected action:{data.Id}");
                    }

                    Stopped.Invoke(this, new(operationResult, string.Empty));
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected state:{state} or Parsing Error");
                }

                return operationResult;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error occurred while processing the action");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, null, ex.Message, ex.StackTrace);
            }
        }).AsAsyncOperation();
    }
}
