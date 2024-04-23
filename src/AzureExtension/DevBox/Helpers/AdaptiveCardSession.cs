// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Drawing;
using System.Text;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Helpers;

public class AdaptiveCardSession : IExtensionAdaptiveCardSession2, IDisposable
{
    private IExtensionAdaptiveCard? _extensionAdaptiveCard;

    private string? _template;

    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs> Stopped = (s, e) => { };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _extensionAdaptiveCard = extensionUI;

        var title = Resources.GetResource("DevBox_AdaptiveCard_Title");
        var description = Resources.GetResource("DevBox_AdaptiveCard_Description");
        var text = Resources.GetResource("DevBox_AdaptiveCard_Text");
        var innerDescription = Resources.GetResource("DevBox_AdaptiveCard_InnerDescription");
        var icon = ConvertIconToDataString("Caution.png");

        var dataJson = new JsonObject
        {
            { "title", title },
            { "description", description },
            { "icon", icon },
            { "loginRequiredText", text },
            { "loginRequiredDescriptionText", innerDescription },
        };

        var operationResult = _extensionAdaptiveCard.Update(
            LoadTemplate(),
            dataJson.ToJsonString(),
            "WaitingForUserSession");

        return operationResult;
    }

    private string LoadTemplate()
    {
        if (!string.IsNullOrEmpty(_template))
        {
            return _template;
        }

        var path = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", "WaitingForUserSessionTemplate.json");
        _template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
        return _template;
    }

    private static string ConvertIconToDataString(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", fileName);
        var imageData = Convert.ToBase64String(File.ReadAllBytes(path.ToString()));
        return imageData;
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs) => throw new NotImplementedException();
}
