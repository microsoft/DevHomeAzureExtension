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
    private readonly IDevBoxManagementService _mgmtSvc;

    public DevBoxProvider(IHost host, IDevBoxManagementService mgmtSvc)
    {
        _host = host;
        _mgmtSvc = mgmtSvc;
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
        return jsonElement.ValueKind != JsonValueKind.Undefined;
    }

    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync(IDeveloperId? developerId)
    {
        var computeSystems = new List<IComputeSystem>();
        _mgmtSvc.DevId = developerId;
        var projectJSONs = await _mgmtSvc.GetAllProjectsAsJsonAsync();

        if (IsValid(projectJSONs))
        {
            Log.Logger()?.ReportInfo($"Found {projectJSONs.EnumerateArray().Count()} projects");
            foreach (var dataItem in projectJSONs.EnumerateArray())
            {
                var project = dataItem.GetProperty("name").ToString();
                var devCenterUri = dataItem.GetProperty("properties").GetProperty("devCenterUri").ToString();

                // Todo: Remove this test
                if (project != "DevBoxUnitTestProject" && project != "EngProdADEPT")
                {
                    continue;
                }

                var boxes = await _mgmtSvc.GetBoxesAsJsonAsync(devCenterUri, project);
                if (IsValid(boxes))
                {
                    Log.Logger()?.ReportInfo($"Found {boxes.EnumerateArray().Count()} boxes for project {project}");

                    foreach (var item in boxes.EnumerateArray())
                    {
                        // Get an empty dev box object and fill in the details
                        var box = _host.Services.GetService<DevBoxInstance>();
                        box?.FillFromJson(item, project, developerId);
                        if (box is not null && box.IsValid)
                        {
                            computeSystems.Add(box);
                        }
                    }
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
