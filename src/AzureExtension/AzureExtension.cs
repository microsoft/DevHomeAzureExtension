// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using AzureExtension.DevBox;
using AzureExtension.Providers;
using AzureExtension.QuickStartPlayground;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension;

[ComVisible(true)]
[Guid("182AF84F-D5E1-469C-9742-536EFEA94630")]
[ComDefaultInterface(typeof(IExtension))]
public sealed class AzureExtension : IExtension
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly IHost _host;
    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(AzureExtension));

    public AzureExtension(ManualResetEvent extensionDisposedEvent, IHost host)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
        _host = host;
    }

    public object? GetProvider(ProviderType providerType)
    {
        switch (providerType)
        {
            case ProviderType.DeveloperId:
                return DeveloperIdProvider.GetInstance();
            case ProviderType.Repository:
                return RepositoryProvider.GetInstance();
            case ProviderType.FeaturedApplications:
                return new object();
            case ProviderType.ComputeSystem:
                return _host.Services.GetService<DevBoxProvider>();
            case ProviderType.QuickStartProject:
                return _host.Services.GetQuickStartProjectProviders();
            case ProviderType.Settings:
                return _host.Services.GetService<SettingsProvider>();
            default:
                _log.Information($"Invalid provider: {providerType}");
                return null;
        }
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
    }
}
