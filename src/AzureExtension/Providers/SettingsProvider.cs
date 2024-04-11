// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Providers;

public class SettingsProvider : ISettingsProvider2
{
    string ISettingsProvider.DisplayName => Resources.GetResource(@"SettingsProviderDisplayName");

    public AdaptiveCardSessionResult GetSettingsAdaptiveCardSession() => throw new NotImplementedException();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public WebViewResult GetSettingsWebView()
    {
        return new WebViewResult("HelloWorld.html");
    }
}
