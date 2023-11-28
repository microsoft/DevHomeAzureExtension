// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using AzureExtension.Contracts;
using DevHomeAzureExtension.DataModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxProvider : IComputeSystemProvider, IDisposable
{
    private readonly IHost _host;

    string IComputeSystemProvider.DefaultComputeSystemProperties
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string DisplayName => "Microsoft DevBox Provider";

    public string Id => "Microsoft.DevBox";

    public string Properties => throw new NotImplementedException();

    public ComputeSystemProviderOperation SupportedOperations => throw new NotImplementedException();

    private bool IsValid(JsonElement jsonElement)
    {
        return jsonElement.ValueKind != JsonValueKind.Undefined;
    }

    public IEnumerable<IComputeSystem> GetComputeSystemsAsync(IDeveloperId? developerId)
    {
        var computeSystems = new List<IComputeSystem>();

        var mgmtSvc = _host.Services.GetService<IDevBoxManagementService>();
        if (mgmtSvc != null)
        {
            return computeSystems;
        }
        else
        {
            // ToDo: Remove throw and add to return object
            Log.Logger()?.ReportError($"Error getting systems: Rest Service not configured");
            throw new ArgumentException($"Rest Service needs to be configured.");
        }
    }

    public IAsyncOperation<CreateComputeSystemResult> CreateComputeSystemAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemsResult> GetComputeSystemsAsync(IDeveloperId developerId, string options)
    {
        return Task.Run(() =>
        {
            var computeSystems = GetComputeSystemsAsync(developerId);
            return new ComputeSystemsResult(computeSystems);
        }).AsAsyncOperation();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
