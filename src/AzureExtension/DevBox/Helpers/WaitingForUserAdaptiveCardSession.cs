// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Drawing;
using System.Text;
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

    public WaitingForUserAdaptiveCardSession(ManualResetEvent resumeEvent)
    {
        this._resumeEvent = resumeEvent;
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
            var state = _extensionAdaptiveCard?.State;
            ProviderOperationResult operationResult;
            if (state == "WaitingForUserSession")
            {
                operationResult = new ProviderOperationResult(ProviderOperationStatus.Success, null, null, null);
                _resumeEvent.Set();
                Stopped?.Invoke(this, new(operationResult, string.Empty));
            }
            else
            {
                operationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, null, "Something went wrong", $"Unexpected state:{_extensionAdaptiveCard?.State}");
            }

            return operationResult;
        }).AsAsyncOperation();
    }
}
