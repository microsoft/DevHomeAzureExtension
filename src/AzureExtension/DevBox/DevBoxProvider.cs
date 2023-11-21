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

    // Returns a DevBox object from a JSON object
    // Can only be reached by GetComputeSystemsAsync() after checking
    // that httpDataClient isn't null hence the warning suppression
#pragma warning disable CS8604 // Possible null reference argument.
    private DevBoxInstance? MakeDevBoxFromJson(JsonElement item, string project, IDeveloperId? devId)
    {
        try
        {
            var uri = item.GetProperty("uri").ToString();
            var name = item.GetProperty("name").ToString();
            var uniqueId = item.GetProperty("uniqueId").ToString();
            var actionState = item.GetProperty("actionState").ToString();
            var vCPUs = item.GetProperty("hardwareProfile").GetProperty("vCPUs").ToString();
            var memory = item.GetProperty("hardwareProfile").GetProperty("memoryGB").ToString();
            var operatingSystem = item.GetProperty("imageReference").GetProperty("operatingSystem").ToString();

            var webUrl = string.Empty;
            var rdpUrl = string.Empty;

            // GetRemoteLaunchURIs(uri, out webUrl, out rdpUrl);
            Log.Logger()?.ReportInfo($"Created box {name} with id {uniqueId} with {actionState}, {vCPUs}, {memory}, {uri}, {operatingSystem}");
            var box = _host.Services.GetRequiredService<DevBoxInstance>();
            box.InstanceFill(name, uniqueId, project, actionState, vCPUs, memory, uri, webUrl, rdpUrl, operatingSystem, devId);
            return box;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Error making DevBox from JSON: {ex.Message}");
            return null;
        }
    }
#pragma warning restore CS8604 // Possible null reference argument.

    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync(IDeveloperId? developerId)
    {
        var computeSystems = new List<IComputeSystem>();

        var mgmtSvc = _host.Services.GetService<IDevBoxManagementService>();
        if (mgmtSvc != null)
        {
            mgmtSvc.DevId = developerId;
            var projectJSONs = await mgmtSvc.GetAllProjectsAsJSONAsync();

            if (IsValid(projectJSONs))
            {
                Log.Logger()?.ReportInfo($"Found {projectJSONs.EnumerateArray().Count()} projects");
                foreach (var dataItem in projectJSONs.EnumerateArray())
                {
                    var project = dataItem.GetProperty("name").ToString();
                    var devCenterUri = dataItem.GetProperty("properties").GetProperty("devCenterUri").ToString();

                    // Todo: Remove this test
                    if (project != "EngProdADEPT")
                    {
                        continue;
                    }

                    var boxes = await mgmtSvc.GetBoxesAsJSONAsync(devCenterUri, project);
                    if (IsValid(boxes))
                    {
                        Log.Logger()?.ReportInfo($"Found {boxes.EnumerateArray().Count()} boxes for project {project}");

                        foreach (var item in boxes.EnumerateArray())
                        {
                            var box = MakeDevBoxFromJson(item, project, developerId);
                            if (box != null)
                            {
                                computeSystems.Add(box);
                            }
                        }
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
            var computeSystems = await GetComputeSystemsAsync(developerId);
            return new ComputeSystemsResult(computeSystems);
        }).AsAsyncOperation();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
