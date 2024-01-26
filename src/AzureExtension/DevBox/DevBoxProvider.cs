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

/// <summary>
/// Implements the IComputeSystemProvider interface to provide DevBoxes as ComputeSystems.
/// </summary>
public class DevBoxProvider : IComputeSystemProvider
{
    private readonly IHost _host;
    private readonly IDevBoxManagementService _devBoxManagementService;

    public DevBoxProvider(IHost host, IDevBoxManagementService mgmtSvc)
    {
        _host = host;
        _devBoxManagementService = mgmtSvc;
    }

    public string DisplayName => "Microsoft DevBox";

    public string Id => "Microsoft.DevBox";

    public string Properties => throw new NotImplementedException();

    ComputeSystemProviderOperations IComputeSystemProvider.SupportedOperations => throw new NotImplementedException();

    /// <summary>
    /// Checks the validity of the JsonElement returned by the DevCenter API.
    /// Validity is determined by the presence of the "value" array property.
    /// i.e. - Projects/DevBoxes are returned as an array of objects.
    /// </summary>
    private bool IsValid(JsonElement jsonElement)
    {
        return jsonElement.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// Adds all DevBoxes for a given project to the list of systems.
    /// </summary>
    /// <param name="data">Object with the properties of the project</param>
    /// <param name="devId">DeveloperID to be used for the authentication token service</param>
    /// <param name="systems">List of valid dev box objects</param>
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

    /// <summary>
    /// Gets the list of DevBoxes for all projects that a user has access to.
    /// </summary>
    /// <param name="developerId">DeveloperID to be used by the authentication token service</param>
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

    /// <summary>
    /// Wrapper for GetComputeSystemsAsync
    /// </summary>
    /// <param name="developerId">DeveloperID to be used by the authentication token service</param>
    /// <param name="options">Unused parameter</param>
    public IAsyncOperation<ComputeSystemsResult> GetComputeSystemsAsync(IDeveloperId developerId, string options)
    {
        return Task.Run(async () =>
        {
            var computeSystems = await GetComputeSystemsAsync(developerId);
            return new ComputeSystemsResult(computeSystems);
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemAdaptiveCardResult> CreateAdaptiveCardSession(IDeveloperId developerId, ComputeSystemAdaptiveCardKind sessionKind) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemAdaptiveCardResult> CreateAdaptiveCardSession(IComputeSystem computeSystem, ComputeSystemAdaptiveCardKind sessionKind) => throw new NotImplementedException();

    public IAsyncOperationWithProgress<CreateComputeSystemResult, ComputeSystemOperationData> CreateComputeSystemAsync(IDeveloperId developerId, string options) => throw new NotImplementedException();
}
