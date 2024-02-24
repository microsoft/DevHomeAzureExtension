// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

/// <summary>
/// Implements the IComputeSystemProvider interface to provide DevBoxes as ComputeSystems.
/// </summary>
public class DevBoxProvider : IComputeSystemProvider
{
    private readonly IDevBoxManagementService _devBoxManagementService;

    private readonly IDevBoxCreationManager _devBoxCreationManager;

    private readonly CreateComputeSystemOperationFactory _createComputeSystemOperationFactory;

    private readonly DevBoxInstanceFactory _devBoxInstanceFactory;

    public DevBoxProvider(
        IDevBoxManagementService mgmtSvc,
        CreateComputeSystemOperationFactory createComputeSystemOperationFactory,
        DevBoxInstanceFactory devBoxInstanceFactory,
        IDevBoxCreationManager devBoxCreationManager,
        IDevBoxOperationWatcher devBoxOperationWatcher)
    {
        _devBoxManagementService = mgmtSvc;
        _createComputeSystemOperationFactory = createComputeSystemOperationFactory;
        _devBoxInstanceFactory = devBoxInstanceFactory;
        _devBoxCreationManager = devBoxCreationManager;
    }

    public string DisplayName => Constants.DevBoxProviderDisplayName;

    public string Id => Constants.DevBoxProviderName;

    public ICreateComputeSystemOperation? ComputeSystemOp { get; set; }

    public ComputeSystemProviderOperations SupportedOperations => ComputeSystemProviderOperations.CreateComputeSystem;

    public Uri Icon => new(Constants.ProviderURI);

    /// <summary>
    /// Adds all DevBoxes for a given project to the list of systems.
    /// </summary>
    /// <param name="devBoxProject">Object with the properties of the project</param>
    /// <param name="devId">DeveloperID to be used for the authentication token service</param>
    /// <param name="systems">List of valid dev box objects</param>
    private async Task ProcessAllDevBoxesInProjectAsync(DevBoxProject devBoxProject, IDeveloperId devId, List<IComputeSystem> systems)
    {
        var devCenterUri = devBoxProject.Properties.DevCenterUri;
        var baseUriStr = $"{devCenterUri}{Constants.Projects}/{devBoxProject.Name}{Constants.DevBoxAPI}";
        var devBoxJsonArray = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(baseUriStr), devId, HttpMethod.Get, null);
        var devBoxMachines = JsonSerializer.Deserialize<DevBoxMachines>(devBoxJsonArray.JsonResponseRoot.ToString(), Constants.JsonOptions);

        if (devBoxMachines?.Value != null && devBoxMachines.Value.Length != 0)
        {
            Log.Logger()?.ReportInfo($"Found {devBoxMachines!.Value.Length} boxes for project {devBoxProject.Name}");

            foreach (var devBoxState in devBoxMachines!.Value)
            {
                if (_devBoxCreationManager.TryGetDevBoxInstanceIfBeingCreated(devBoxState.UniqueId, out var devBox))
                {
                    // If the Dev Box's creation operation or its provisioning state are being tracked by us, then don't make a new instance.
                    // Add the one we're tracking. This is to avoid adding the same Dev Box twice. E.g User clicks Dev Homes refresh button while
                    // the Dev Box is being created.
                    systems.Add(devBox!);
                    continue;
                }

                var newDevBoxInstance = _devBoxInstanceFactory(devId, devBoxState);

                if (newDevBoxInstance.IsDevBoxBeingCreatedOrProvisioned)
                {
                    // DevBox is being created but we aren't tracking it yet. So we'll start tracking its provisioning state until its fully provisioned.
                    // It was likely created by Dev Portals UI or some other non-Dev Home related UI.
                    _devBoxCreationManager.StartDevBoxProvisioningStateMonitor(newDevBoxInstance.AssociatedDeveloperId, newDevBoxInstance);
                }

                systems.Add(newDevBoxInstance);
            }
        }
    }

    /// <summary>
    /// Gets the list of DevBoxes for all projects that a user has access to.
    /// </summary>
    /// <param name="developerId">DeveloperId to be used by the authentication token service</param>
    public async Task<IEnumerable<IComputeSystem>> GetComputeSystemsAsync(IDeveloperId developerId)
    {
        var requestContent = new StringContent(Constants.ARGQuery, Encoding.UTF8, "application/json");
        var result = await _devBoxManagementService.HttpsRequestToManagementPlane(new Uri(Constants.ARGQueryAPI), developerId, HttpMethod.Post, requestContent);
        var devBoxProjects = JsonSerializer.Deserialize<DevBoxProjects>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
        var computeSystems = new List<IComputeSystem>();

        if (devBoxProjects?.Data != null)
        {
            Log.Logger()?.ReportInfo($"Found {devBoxProjects.Data.Length} projects");
            foreach (var project in devBoxProjects.Data)
            {
                try
                {
                    await ProcessAllDevBoxesInProjectAsync(project, developerId, computeSystems);
                }
                catch (Exception ex)
                {
                    Log.Logger()?.ReportError($"Error processing project {project.Name}", ex);
                }
            }
        }

        return computeSystems;
    }

    /// <summary>
    /// Wrapper for GetComputeSystemsAsync
    /// </summary>
    /// <param name="developerId">DeveloperId to be used by the authentication token service</param>
    /// <param name="options">Unused parameter</param>
    public IAsyncOperation<ComputeSystemsResult> GetComputeSystemsAsync(IDeveloperId developerId, string options)
    {
        return Task.Run(async () =>
        {
            try
            {
                ArgumentNullException.ThrowIfNull(developerId);

                var computeSystems = await GetComputeSystemsAsync(developerId);
                return new ComputeSystemsResult(computeSystems);
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Unable to get all Dev Boxes", ex);
                return new ComputeSystemsResult(ex, ex.Message);
            }
        }).AsAsyncOperation();
    }

    public ComputeSystemAdaptiveCardResult CreateAdaptiveCardSession(IDeveloperId developerId, ComputeSystemAdaptiveCardKind sessionKind)
    {
        var exception = new NotImplementedException();
        return new ComputeSystemAdaptiveCardResult(exception, exception.Message);
    }

    public ComputeSystemAdaptiveCardResult CreateAdaptiveCardSession(IComputeSystem computeSystem, ComputeSystemAdaptiveCardKind sessionKind)
    {
        var exception = new NotImplementedException();
        return new ComputeSystemAdaptiveCardResult(exception, exception.Message);
    }

    public ICreateComputeSystemOperation? CreateComputeSystem(IDeveloperId developerId, string options)
    {
        if (developerId is null || string.IsNullOrEmpty(options))
        {
            return null;
        }

        var parameters = JsonSerializer.Deserialize<DevBoxCreationParameters>(options, Constants.JsonOptions)!;

        return _createComputeSystemOperationFactory(developerId, parameters);
    }
}
