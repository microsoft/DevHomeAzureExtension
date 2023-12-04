// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using AzureExtension.Contracts;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxProvider : IComputeSystemProvider
{
    private readonly IHost _host;
    private readonly IDevBoxManagementService _devBoxManagementService;

    public DevBoxProvider(IHost host, IDevBoxManagementService mgmtSvc)
    {
        _host = host;
        _devBoxManagementService = mgmtSvc;
    }

    string IComputeSystemProvider.DefaultComputeSystemProperties
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string DisplayName => "Microsoft DevBox Provider";

    public string Id => "Microsoft.DevBox";

    public string Properties => throw new NotImplementedException();

    // No create operation supported
    public ComputeSystemProviderOperation SupportedOperations => 0x0;

    private bool IsValid(JsonElement jsonElement)
    {
        return jsonElement.ValueKind == JsonValueKind.Array;
    }

    private async Task ProcessAllDevBoxesInProjectAsync(JsonElement data, IDeveloperId? devId, List<IComputeSystem> systems)
    {
        var project = data.GetProperty("name").ToString();
        var devCenterUri = data.GetProperty("properties").GetProperty("devCenterUri").ToString();

        // Todo: Remove this test in Prod
        if (project != "DevBoxUnitTestProject" && project != "EngProdADEPT")
        {
            return;
        }

        var devBoxes = await _devBoxManagementService.GetDevBoxesAsJsonAsync(devCenterUri, project);
        if (IsValid(devBoxes))
        {
            Log.Logger()?.ReportInfo($"Found {devBoxes.EnumerateArray().Count()} boxes for project {project}");

            foreach (var item in devBoxes.EnumerateArray())
            {
                // Get an empty dev box object and fill in the details
                var devBox = _host.Services.GetService<DevBoxInstance>();
                devBox?.FillFromJson(item, project, devId);
                if (devBox is not null && devBox.IsValid)
                {
                    systems.Add(devBox);
                }
            }
        }
    }

    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync(IDeveloperId? developerId)
    {
        _devBoxManagementService.DevId = developerId;
        var projectJSONs = await _devBoxManagementService.GetAllProjectsAsJsonAsync();
        var computeSystems = new List<IComputeSystem>();

        if (IsValid(projectJSONs))
        {
            Log.Logger()?.ReportInfo($"Found {projectJSONs.EnumerateArray().Count()} projects");
            foreach (var dataItem in projectJSONs.EnumerateArray())
            {
                try
                {
                    await ProcessAllDevBoxesInProjectAsync(dataItem, developerId, computeSystems);
                }
                catch (Exception ex)
                {
                    dataItem.TryGetProperty("name", out var name);
                    Log.Logger()?.ReportError($"Error processing project {name.ToString()}: {ex.ToString}");
                }
            }
        }

        return computeSystems;
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
}
