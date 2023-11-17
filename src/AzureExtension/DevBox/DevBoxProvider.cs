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

    public string Id => "182AF84F-D5E1-469C-9742-536EFEA94630";

    public string Properties => throw new NotImplementedException();

    public ComputeSystemProviderOperation SupportedOperations => 0x0;

    private bool IsValid(JsonElement jsonElement)
    {
        return jsonElement.ValueKind != JsonValueKind.Undefined;
    }

    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync()
    {
        var computeSystems = new List<IComputeSystem>();

        var restSvc = _host.Services.GetService<IDevBoxRESTService>();
        if (restSvc != null)
        {
            var projectJSONs = await restSvc.GetAllProjectsAsJSONAsync();

            if (IsValid(projectJSONs))
            {
                Log.Logger()?.ReportInfo($"Found {projectJSONs.EnumerateArray().Count()} projects");
                foreach (var dataItem in projectJSONs.EnumerateArray())
                {
                    var project = dataItem.GetProperty("name").ToString();
                    var devCenterUri = dataItem.GetProperty("properties").GetProperty("devCenterUri").ToString();
                    var boxes = await restSvc.GetBoxesAsJSONAsync(devCenterUri, project);
                    if (IsValid(boxes))
                    {
                        Log.Logger()?.ReportInfo($"Found {boxes.EnumerateArray().Count()} boxes for project {project}");

                        // foreach (var item in boxes.EnumerateArray())
                        // {
                            // var box = MakeDevBoxFromJson(item, project);
                            // if (box != null)
                            // {
                            //     computeSystems.Add(box);
                            // }
                        // }
                    }
                }
            }

            return computeSystems;
        }
        else
        {
            Log.Logger()?.ReportError($"Error getting systems: Rest Service not configured");
            throw new ArgumentException($"Rest Service needs to be configured.");
        }
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
