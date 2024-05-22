// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Helpers;

public class WaitingForUserAdaptiveCardSession : IExtensionAdaptiveCardSession2, IDisposable
{
    private IExtensionAdaptiveCard? _extensionAdaptiveCard;

    private string? _template;

    private ManualResetEvent _resumeEvent;

    private ManualResetEvent _launchEvent;

    private JsonSerializerOptions _serializerOptions = new()
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
                if (state == "WaitingForUserSession" && data != null)
                {
                    switch (data.Id)
                    {
                        case "launchAction":
                            operationResult = new ProviderOperationResult(ProviderOperationStatus.Success, null, null, null);
                            _launchEvent.Set();
                            _resumeEvent.Set();
                            break;
                        case "resumeAction":
                            operationResult = new ProviderOperationResult(ProviderOperationStatus.Success, null, null, null);
                            _resumeEvent.Set();
                            break;
                        default:
                            operationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, null, "Something went wrong", $"Unexpected action:{data.Id}");
                            break;
                    }

                    Stopped?.Invoke(this, new(operationResult, string.Empty));
                }
                else
                {
                    operationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, null, "Something went wrong", $"Unexpected state:{_extensionAdaptiveCard?.State} or Parsing Error");
                }

                return operationResult;
            }
            catch (Exception ex)
            {
                return new ProviderOperationResult(ProviderOperationStatus.Failure, null, ex.Message, ex.StackTrace);
            }
        }).AsAsyncOperation();
    }
}
