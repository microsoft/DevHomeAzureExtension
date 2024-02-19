// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

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

    public DevBoxCreationManager(IDevBoxManagementService devBoxManagementService, DevBoxInstanceFactory devBoxInstanceFactory)
    {
        _devBoxManagementService = devBoxManagementService;
        _devBoxInstanceFactory = devBoxInstanceFactory;
    }

    /// <inheritdoc cref="IDevBoxCreationManager.StartCreateDevBoxOperation"/>
    public async Task StartCreateDevBoxOperation(CreateComputeSystemOperation operation, IDeveloperId developerId, string userOptions)
    {
        var parameters = JsonSerializer.Deserialize<DevBoxCreationParameters>(userOptions, Constants.JsonOptions)!;

        try
        {
            operation.UpdateProgress(Resources.GetResource(SendingCreationRequestProgressKey), Constants.IndefiniteProgress);

            var result = await _devBoxManagementService.CreateDevBox(parameters, developerId);
            operation.UpdateProgress(Resources.GetResource(CreationResponseReceivedProgressKey, parameters.DevBoxName, parameters.ProjectName), Constants.IndefiniteProgress);

            var devBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;
            var devBox = _devBoxInstanceFactory(developerId, devBoxState);

            operation.UpdateProgress(Resources.GetResource(DevCenterCreationStartedProgressKey, parameters.DevBoxName, parameters.ProjectName), Constants.IndefiniteProgress);

            // We can now send the Dev Box instance to Dev Home. The state will be "Creating", as the operation is still ongoing in the Dev Center.
            operation.CompleteWithSuccess(devBox);

            var callback = DevCenterLongRunningOperationCallback(devBox);

            // Now we can start querying the Dev Center for the creation status of the Dev Box operation. This operation will continue until the Dev Box is ready for use.
            _devBoxManagementService.StartDevCenterOperationMonitor(developerId, result.ResponseHeader, Constants.FiveMinutePeriod, callback);

            // At this point the DevBox is partially created in the cloud. However the DevBox is not ready for use. Querying for all Dev Box will
            // return this DevBox via Json with its provisioningState set to "Provisioning". So, we'll keep track of the operation.
            AddDevBoxFromMap(devBox);
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(DevBoxCreationManagerName, $"unable to create the Dev Box with user options: {userOptions}", ex);
            operation.CompleteWithFailure(ex, Resources.GetResource(CreationErrorProgressKey, parameters.DevBoxName, parameters.ProjectName));
        }
    }

    private Action<DevBoxLongRunningOperation> DevCenterLongRunningOperationCallback(DevBoxInstance devBox)
    {
        return (DevBoxLongRunningOperation longRunningOperation) =>
            {
                switch (longRunningOperation.Status)
                {
                    case Constants.DevBoxOperationNotStartedState:
                    case Constants.DevBoxRunningState:
                        break;
                    case Constants.DevBoxOperationSucceededState:
                        RemoveDevBoxFromMap(devBox);

                        // The Dev Box is now ready for use. No need to request a new Dev Box from the Dev Center. We can update the state for Dev Homes UI.
                        devBox.DevBoxState.ProvisioningState = Constants.DevBoxOperationSucceededState;
                        devBox.UpdateStateForUI();
                        break;
                    default:
                        Log.Logger()?.ReportError(DevBoxCreationManagerName, $"Dev Box creation monitoring stopped unexpectedly. {longRunningOperation}");
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

        AddDevBoxFromMap(devBox);
        _devBoxManagementService.StartDevBoxProvisioningStatusMonitor(developerId, Constants.FiveMinutePeriod, devBox, RemoveDevBoxFromMap);
    }

    private void RemoveDevBoxFromMap(DevBoxInstance devBox)
    {
        lock (_creationLock)
        {
            _devBoxesBeingCreated.Remove(devBox.Id);
        }
    }

    private void AddDevBoxFromMap(DevBoxInstance devBox)
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
            if (_devBoxesBeingCreated.TryGetValue(id, out var value))
            {
                devBox = value;
                return true;
            }

            return false;
        }
    }
}
