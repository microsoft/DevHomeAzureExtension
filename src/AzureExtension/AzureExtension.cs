// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Runtime.InteropServices;
using AzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension;

[ComVisible(true)]
[Guid("182AF84F-D5E1-469C-9742-536EFEA94630")]
[ComDefaultInterface(typeof(IExtension))]
public sealed class AzureExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly IHost _host;
    private readonly WebServer.WebServer _webServer;

    public AzureExtension(ManualResetEvent extensionDisposedEvent, IHost host)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
        _host = host;

        var webcontentPath = Path.Combine(AppContext.BaseDirectory, "WebContent");
        _webServer = new WebServer.WebServer(webcontentPath);

        _webServer.RegisterRouteHandler("/api/test", HandleRequest);

        Console.WriteLine($"GitHubExtension is running on port {_webServer.Port}");
        var url = $"http://localhost:{_webServer.Port}/HelloWorld.html";
        Console.WriteLine($"Navigate to: {url}");
    }

    public object? GetProvider(ProviderType providerType)
    {
        switch (providerType)
        {
            case ProviderType.DeveloperId:
                return DeveloperIdProvider.GetInstance();
            case ProviderType.Repository:
                return new RepositoryProvider();
            case ProviderType.FeaturedApplications:
                return new object();
            case ProviderType.ComputeSystem:
                return _host.Services.GetService<DevBoxProvider>();
            case ProviderType.Settings:
                return new SettingsProvider();
            default:
                Providers.Log.Logger()?.ReportInfo("Invalid provider");
                return null;
        }
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
        _webServer.Dispose();
    }

    public bool HandleRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        Console.WriteLine("Received request for /api/test");
        return true;
    }
}
