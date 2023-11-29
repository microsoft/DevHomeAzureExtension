// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Runtime.InteropServices;
using AzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension;

[ComVisible(true)]
[Guid("182AF84F-D5E1-469C-9742-536EFEA94630")]
[ComDefaultInterface(typeof(IExtension))]
public sealed class AzureExtension : IExtension
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly IHost _host;

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
                return new RepositoryProvider();
            case ProviderType.FeaturedApplications:
                return new object();
            case ProviderType.ComputeSystem:
                return new DevBoxProvider(_host);
            default:
                Providers.Log.Logger()?.ReportInfo("Invalid provider");
                return null;
        }
    }

    public void Dispose()
    {
        _extensionDisposedEvent.Set();
    }
}
