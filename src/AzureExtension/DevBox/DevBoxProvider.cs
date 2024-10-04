// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Models;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.DevBox;

/// <summary>
/// Implements the IComputeSystemProvider interface to provide DevBoxes as ComputeSystems.
/// </summary>
public class DevBoxProvider : IComputeSystemProvider
{
    private readonly IDevBoxManagementService _devBoxManagementService;

    private readonly IDevBoxCreationManager _devBoxCreationManager;

    private readonly CreateComputeSystemOperationFactory _createComputeSystemOperationFactory;

    private readonly DevBoxInstanceFactory _devBoxInstanceFactory;

    private readonly Dictionary<string, DevBoxProjects> _devBoxProjectsMap = new();

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevBoxProvider));

    private readonly Dictionary<string, List<DevBoxProjectAndPoolContainer>> _devBoxProjectAndPoolsMap = new();

    private readonly Dictionary<string, List<IComputeSystem>> _cachedDevBoxesMap = new();

    private readonly ConcurrentBag<IComputeSystem> _devBoxes = new();

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

    public string Id => Constants.DevBoxProviderId;

    public ComputeSystemProviderOperations SupportedOperations => ComputeSystemProviderOperations.CreateComputeSystem;

    public Uri Icon => new(Constants.ProviderIcon);

    /// <summary>
    /// Adds all DevBoxes for a given project to the list of systems.
    /// </summary>
    /// <param name="devBoxProject">Object with the properties of the project</param>
    /// <param name="devId">DeveloperID to be used for the authentication token service</param>
    private async Task ProcessAllDevBoxesInProjectAsync(DevBoxProject devBoxProject, IDeveloperId devId)
    {
        var devCenterUri = devBoxProject.Properties.DevCenterUri;
        var baseUriStr = $"{devCenterUri}{Constants.Projects}/{devBoxProject.Name}{Constants.DevBoxAPI}";
        var devBoxJsonArray = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(baseUriStr), devId, HttpMethod.Get, null);
        var devBoxMachines = JsonSerializer.Deserialize<DevBoxMachines>(devBoxJsonArray.JsonResponseRoot.ToString(), Constants.JsonOptions);

        if (devBoxMachines?.Value != null && devBoxMachines.Value.Length != 0)
        {
            _log.Information($"Found {devBoxMachines!.Value.Length} boxes for project {devBoxProject.Name}");

            foreach (var devBoxState in devBoxMachines!.Value)
            {
                if (_devBoxCreationManager.TryGetDevBoxInstanceIfBeingCreated(devBoxState.UniqueId, out var devBox))
                {
                    _log.Information($"DevBox with name: {devBox!.DevBoxState.Name} found and is currently being created and monitored by Dev Box provider");

                    // If the Dev Box's creation operation or its provisioning state are being tracked by us, then don't make a new instance.
                    // Add the one we're tracking. This is to avoid adding the same Dev Box twice. E.g User clicks Dev Homes refresh button while
                    // the Dev Box is being created.
                    _devBoxes.Add(devBox!);
                    continue;
                }

                var newDevBoxInstance = _devBoxInstanceFactory(devId, devBoxState);

                if (newDevBoxInstance.IsDevBoxBeingCreatedOrProvisioned)
                {
                    _log.Information(
                        $"DevBox with name: {newDevBoxInstance!.DevBoxState.Name} is currently being provisioned and is not monitored by the DevBox provider. Adding DevBox To Operation watcher");

                    // DevBox is being created but we aren't tracking it yet. So we'll start tracking its provisioning state until its fully provisioned.
                    // It was likely created by Dev Portals UI or some other non-Dev Home related UI.
                    _devBoxCreationManager.StartDevBoxProvisioningStateMonitor(newDevBoxInstance.AssociatedDeveloperId, newDevBoxInstance);
                }
                else
                {
                    await newDevBoxInstance.LoadWindowsAppParameters();
                }

                _devBoxes.Add(newDevBoxInstance);
            }
        }
    }

    /// <summary>
    /// Gets the list of DevBoxes for all projects that a user has access to.
    /// </summary>
    /// <param name="developerId">DeveloperId to be used by the authentication token service</param>
    public async Task<IEnumerable<IComputeSystem>> GetDevBoxesAsync(IDeveloperId developerId)
    {
        var devBoxProjects = await GetDevBoxProjectsAsync(developerId);

        if (devBoxProjects?.Data != null)
        {
            _log.Information($"Found {devBoxProjects.Data.Length} projects");
            _devBoxes.Clear();
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2));
            var token = cancellationTokenSource.Token;

            await Parallel.ForEachAsync(devBoxProjects.Data, async (project, token) =>
            {
                try
                {
                    await ProcessAllDevBoxesInProjectAsync(project, developerId);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Error processing project {project.Name}");
                }
            });
        }

        // update the cache every time we retrieve new Dev Boxes. This is used so in the creation flow we don't need
        // to retrieve the Dev Boxes again if the user already retrieved them in the environments page in Dev Home
        var uniqueUserId = GetUniqueDeveloperId(developerId);
        _cachedDevBoxesMap[uniqueUserId] = _devBoxes.ToList();
        return _cachedDevBoxesMap[uniqueUserId];
    }

    /// <summary>
    /// Wrapper for GetComputeSystemsAsync
    /// </summary>
    /// <param name="developerId">DeveloperId to be used by the authentication token service</param>
    public IAsyncOperation<ComputeSystemsResult> GetComputeSystemsAsync(IDeveloperId developerId)
    {
        return Task.Run(async () =>
        {
            try
            {
                ArgumentNullException.ThrowIfNull(developerId);
                ArgumentNullException.ThrowIfNullOrEmpty(developerId.LoginId);

                var start = DateTime.Now;
                _log.Information($"Attempting to retrieving all Dev Boxes for {developerId.LoginId}");

                var computeSystems = await GetDevBoxesAsync(developerId);

                _log.Information($"Successfully retrieved all Dev Boxes for {developerId.LoginId}, in {DateTime.Now - start}");
                return new ComputeSystemsResult(computeSystems);
            }
            catch (Exception ex)
            {
                var errorMessage = Constants.OperationsDefaultErrorMsg;
                if (ex.InnerException != null && ex.InnerException.Message.Contains("Account has previously been signed out of this application"))
                {
                    errorMessage = Resources.GetResource(Constants.RetrievalFailKey, developerId.LoginId) + Resources.GetResource(Constants.SessionExpiredKey);
                }
                else if (ex.Message.Contains("A passthrough token was detected without proper resource provider context"))
                {
                    errorMessage = Resources.GetResource(Constants.RetrievalFailKey, developerId.LoginId) + Resources.GetResource(Constants.UnconfiguredKey);
                }
                else if (ex is ArgumentException ae)
                {
                    errorMessage = Resources.GetResource(Constants.NoDefaultUserFailKey);
                }
                else if (ex.Message.Contains("WAM Error"))
                {
                    errorMessage = SimplifyWAMError(ex.Message);
                }
                else
                {
                    errorMessage = Resources.GetResource(Constants.RetrievalFailKey, developerId.LoginId) + ex.Message;
                }

                _log.Error(ex, errorMessage);
                return new ComputeSystemsResult(ex, errorMessage, string.Empty);
            }
        }).AsAsyncOperation();
    }

    // Handle common WAM errors
    // Reference: https://learn.microsoft.com/en-us/entra/msal/dotnet/advanced/exceptions/wam-errors
    private string SimplifyWAMError(string errorMessage)
    {
        if (errorMessage.Contains("3399614467"))
        {
            return Constants.DevBoxWAMError1 + "\n" + Resources.GetResource(Constants.DevBoxWAMErrorRefer);
        }
        else if (errorMessage.Contains("3399614476"))
        {
            return Constants.DevBoxWAMError2 + "\n" + Resources.GetResource(Constants.DevBoxWAMErrorRefer);
        }
        else
        {
            return errorMessage + "\n" + Resources.GetResource(Constants.DevBoxWAMErrorRefer);
        }
    }

    public ICreateComputeSystemOperation? CreateCreateComputeSystemOperation(IDeveloperId developerId, string inputJson)
    {
        _log.Information($"Attempting to create CreateComputeSystemOperation for {developerId.LoginId}");

        if (developerId is null || string.IsNullOrEmpty(inputJson))
        {
            _log.Error($"Unable to create Dev Box with DeveloperId: '{developerId?.LoginId ?? "null"}' and inputJson '{inputJson ?? "null"}'");
            return null;
        }

        var parameters = JsonSerializer.Deserialize<DevBoxCreationParameters>(inputJson, Constants.JsonOptions)!;

        return _createComputeSystemOperationFactory(developerId, parameters);
    }

    public ComputeSystemAdaptiveCardResult CreateAdaptiveCardSessionForDeveloperId(IDeveloperId developerId, ComputeSystemAdaptiveCardKind sessionKind)
    {
        return Task.Run(async () =>
        {
            try
            {
                _log.Information($"Attempting to create adaptive card session for {developerId?.LoginId ?? "null"}");

                ArgumentNullException.ThrowIfNull(developerId);

                var uniqueId = GetUniqueDeveloperId(developerId);
                if (!_devBoxProjectAndPoolsMap.TryGetValue(uniqueId, out var _))
                {
                    _log.Information($"No cached Dev Boxes found for {developerId.LoginId}, getting new Dev Boxes");
                    await GetDevBoxesAsync(developerId);
                    _devBoxProjectAndPoolsMap[uniqueId] = await
                        _devBoxManagementService.GetAllProjectsToPoolsMappingAsync(_devBoxProjectsMap[uniqueId]!, developerId);
                }

                return new ComputeSystemAdaptiveCardResult(new CreationAdaptiveCardSession(_devBoxProjectAndPoolsMap[uniqueId], _cachedDevBoxesMap[uniqueId]));
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unable to get the adaptive card session for the provided developerId");
                return new ComputeSystemAdaptiveCardResult(ex, Constants.OperationsDefaultErrorMsg, ex.Message);
            }
        }).GetAwaiter().GetResult();
    }

    public ComputeSystemAdaptiveCardResult CreateAdaptiveCardSessionForComputeSystem(IComputeSystem computeSystem, ComputeSystemAdaptiveCardKind sessionKind)
    {
        var exception = new NotImplementedException();
        return new ComputeSystemAdaptiveCardResult(exception, Constants.OperationsDefaultErrorMsg, exception.Message);
    }

    private async Task<DevBoxProjects> GetDevBoxProjectsAsync(IDeveloperId developerId)
    {
        _log.Information($"Attempting to get all projects for {developerId?.LoginId ?? "null"}");

        ArgumentNullException.ThrowIfNull(developerId);

        var uniqueUserId = GetUniqueDeveloperId(developerId);

        if (_devBoxProjectsMap.TryGetValue(uniqueUserId, out var projects))
        {
            _log.Information($"Found cached projects for {developerId.LoginId}, returning cached projects");
            return projects;
        }

        var requestContent = new StringContent(Constants.ARGQuery, Encoding.UTF8, "application/json");
        var result = await _devBoxManagementService.HttpsRequestToManagementPlane(new Uri(Constants.ARGQueryAPI), developerId, HttpMethod.Post, requestContent);
        var devBoxProjects = JsonSerializer.Deserialize<DevBoxProjects>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);

        _devBoxProjectsMap.Add(uniqueUserId, devBoxProjects!);
        return devBoxProjects!;
    }

    private string GetUniqueDeveloperId(IDeveloperId developerId)
    {
        return $"{developerId.LoginId}#{developerId.Url}";
    }
}
