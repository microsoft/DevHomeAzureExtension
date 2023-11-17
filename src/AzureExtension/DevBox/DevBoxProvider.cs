// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxProvider : IComputeSystemProvider, IDisposable
{
    private readonly IHost _host;

    public DevBoxProvider(IHost host)
    {
        _host = host;
    }

    string IComputeSystemProvider.DefaultComputeSystemProperties
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string DisplayName => "DevBox Provider";

    public string Id => throw new NotImplementedException();

    public string Properties => throw new NotImplementedException();

    public ComputeSystemProviderOperation SupportedOperations => 0x0;

    public string GetTest()
    {
        var restSvc = _host.Services.GetService<IDevBoxRESTService>();
        if (restSvc != null)
        {
            return restSvc.GetBoxes().ToString();
        }

        return string.Empty;
    }

    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync()
    {
        var computeSystems = new List<IComputeSystem>();

        // var restSvc = _host.Services.GetService<IDevBoxRESTService>();

        // Add a 1 sec delay
        await Task.Delay(10);
        return computeSystems;
    }

    public IAsyncOperation<CreateComputeSystemResult> CreateComputeSystemAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemsResult> GetComputeSystemsAsync(IDeveloperId developerId, string options)
    {
        return Task.Run(async () =>
        {
            var computeSystems = await GetComputeSystemsAsync();
            return new ComputeSystemsResult(computeSystems);
        }).AsAsyncOperation();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
