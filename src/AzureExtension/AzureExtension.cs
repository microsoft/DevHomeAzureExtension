// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using DevHomeAzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Providers;
using DevHomeAzureExtension.QuickStartPlayground;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension;

[ComVisible(true)]
#if CANARY_BUILD
[Guid("0F3605DE-92CF-4FE0-9BCD-A3519EA67D1B")]
#elif STABLE_BUILD
[Guid("182AF84F-D5E1-469C-9742-536EFEA94630")]
#else
[Guid("28A2AB40-26EA-48BF-A8BC-49FEC9B7C18C")]
#endif
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
