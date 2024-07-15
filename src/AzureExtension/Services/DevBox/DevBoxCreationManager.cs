// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Models;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using DevBoxConstants = DevHomeAzureExtension.DevBox.Constants;

namespace DevHomeAzureExtension.Services.DevBox;

public class DevBoxCreationManager : IDevBoxCreationManager
{
    private const string CreationResponseReceivedProgressKey = "DevBox_CreationResponseReceived";

    private const string SendingCreationRequestProgressKey = "DevBox_SendingCreationRequest";

    private const string DevCenterCreationStartedProgressKey = "DevBox_DevCenterCreationStartedDevBox";

    private const string CreationErrorProgressKey = "DevBox_CreationError";

    private readonly IDevBoxManagementService _devBoxManagementService;

    private readonly Dictionary<string, DevBoxInstance> _devBoxesBeingCreated = new();

    private readonly object _creationLock = new();

    private const string DevBoxCreationManagerName = nameof(DevBoxCreationManager);

    private readonly DevBoxInstanceFactory _devBoxInstanceFactory;

    private readonly IDevBoxOperationWatcher _devBoxOperationWatcher;

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevBoxCreationManager));

    public DevBoxCreationManager(IDevBoxManagementService devBoxManagementService, IDevBoxOperationWatcher devBoxOperationWatcher,  DevBoxInstanceFactory devBoxInstanceFactory)
    {
        _devBoxManagementService = devBoxManagementService;
        _devBoxInstanceFactory = devBoxInstanceFactory;
        _devBoxOperationWatcher = devBoxOperationWatcher;
    }

    /// <inheritdoc cref="IDevBoxCreationManager.StartCreateDevBoxOperation"/>
    public async Task<CreateComputeSystemResult> StartCreateDevBoxOperation(CreateComputeSystemOperation operation, IDeveloperId developerId, DevBoxCreationParameters parameters)
    {
        try
        {
            _log.Information($"Starting the create DevBox operation for new environment with new: {parameters.NewEnvironmentName}");
            operation.UpdateProgress(Resources.GetResource(SendingCreationRequestProgressKey), DevBoxConstants.IndefiniteProgress);

            var result = await _devBoxManagementService.CreateDevBox(parameters, developerId);
            operation.UpdateProgress(Resources.GetResource(CreationResponseReceivedProgressKey, parameters.NewEnvironmentName, parameters.ProjectName), DevBoxConstants.IndefiniteProgress);

            var devBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), DevBoxConstants.JsonOptions)!;
            var devBox = _devBoxInstanceFactory(developerId, devBoxState);

            operation.UpdateProgress(Resources.GetResource(DevCenterCreationStartedProgressKey, parameters.NewEnvironmentName, parameters.ProjectName), DevBoxConstants.IndefiniteProgress);

            var callback = DevCenterLongRunningOperationCallback(devBox);

            // Now we can start querying the Dev Center for the creation status of the Dev Box operation. This operation will continue until the Dev Box is ready for use.
            var operationUri = result.ResponseHeader.OperationLocation;
            var operationId = Guid.Parse(operationUri!.Segments.Last());

            _devBoxOperationWatcher.StartDevCenterOperationMonitor(developerId, operationUri!, operationId, DevBoxActionToPerform.Create, callback);

            // At this point the DevBox is partially created in the cloud. However the DevBox is not ready for use. Querying for all Dev Box will
            // return this DevBox via Json with its provisioningState set to "Provisioning". So, we'll keep track of the operation.
            AddDevBoxToMap(devBox);

            // We can now send the Dev Box instance to Dev Home. The state will be "Creating", as the operation is still ongoing in the Dev Center.
            return new CreateComputeSystemResult(devBox);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"unable to create the Dev Box with user options: {parameters}");
            return new CreateComputeSystemResult(ex, Resources.GetResource(CreationErrorProgressKey, parameters.NewEnvironmentName, parameters.ProjectName, ex.Message), ex.Message);
        }
    }

    private Action<DevCenterOperationStatus?> DevCenterLongRunningOperationCallback(DevBoxInstance devBox)
    {
        return (DevCenterOperationStatus? status) =>
            {
                _log.Information($"Long running operation status: '{status}' received for DevBox: {devBox.DisplayName}, Id: {devBox.Id}.");
                switch (status)
                {
                    case DevCenterOperationStatus.NotStarted:
                    case DevCenterOperationStatus.Running:
                        break;
                    case DevCenterOperationStatus.Succeeded:
                        RemoveDevBoxFromMap(devBox);

                        // Update the Dev Box Machine state with the updated state from the Dev Center.
                        devBox.OperationCallback(DevCenterOperationStatus.Succeeded);
                        break;
                    default:
                        RemoveDevBoxFromMap(devBox);
                        devBox.UpdateStateForUI();
                        break;
                }
            };
    }

    /// <inheritdoc cref="IDevBoxCreationManager.StartDevBoxProvisioningStateMonitor"/>
    public void StartDevBoxProvisioningStateMonitor(IDeveloperId developerId, DevBoxInstance devBox)
    {
        // Don't add the Dev Box to the map if its already there.
        if (TryGetDevBoxInstanceIfBeingCreated(devBox.Id, out _))
        {
            return;
        }

        AddDevBoxToMap(devBox);
        _devBoxOperationWatcher.StartDevBoxProvisioningStatusMonitor(developerId, DevBoxActionToPerform.Create, devBox, RemoveDevBoxFromMap);
    }

    private void RemoveDevBoxFromMap(DevBoxInstance devBox)
    {
        lock (_creationLock)
        {
            _devBoxesBeingCreated.Remove(devBox.Id);
        }
    }

    private void AddDevBoxToMap(DevBoxInstance devBox)
    {
        lock (_creationLock)
        {
            _devBoxesBeingCreated.Add(devBox.Id, devBox);
        }
    }

    public bool TryGetDevBoxInstanceIfBeingCreated(string id, out DevBoxInstance? devBox)
    {
        devBox = null;

        lock (_creationLock)
        {
            // The user previously chose to delete the Dev Box while it was being created. So it is no longer being
            // watched and we need to remove it from the creation map.
            if (!_devBoxOperationWatcher.IsIdBeingWatched(Guid.Parse(id)) && _devBoxesBeingCreated.ContainsKey(id))
            {
                _devBoxesBeingCreated.Remove(id);
                return false;
            }

            if (_devBoxesBeingCreated.TryGetValue(id, out var value))
            {
                devBox = value;
                return true;
            }

            return false;
        }
    }
}
