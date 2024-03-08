// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Helpers;
using AzureExtension.DevBox.Models;
using AzureExtension.Services.DevBox;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AzureExtension.DevBox;

public delegate DevBoxInstance DevBoxInstanceFactory(IDeveloperId developerId, DevBoxMachineState devBoxJson);

public enum DevBoxActionToPerform
{
    None,
    Start,
    Stop,
    Restart,
    Delete,
    Create,
    Repair,
}

/// <summary>
/// As the actual implementation of IComputeSystem, DevBoxInstance is an instance of a DevBox.
/// It contains the DevBox details such as name, id, state, CPU, memory, and OS. And all the
/// operations that can be performed on the DevBox.
/// </summary>
public class DevBoxInstance : IComputeSystem
{
    private readonly IDevBoxAuthService _authService;

    private readonly IDevBoxManagementService _devBoxManagementService;

    private readonly IDevBoxOperationWatcher _devBoxOperationWatcher;

    private const string SupplementalDisplayNamePrefix = "DevBox_SupplementalDisplayNamePrefix";

    private const string OsPropertyName = "DevBox_OsDisplayText";

    private const string DevBoxInstanceName = "DevBoxInstance";

    private const string DevBoxMultipleConcurrentOperationsNotSupportedKey = "DevBox_MultipleConcurrentOperationsNotSupport";

    private readonly object _operationLock = new();

    public bool IsOperationInProgress { get; private set; }

    public IDeveloperId AssociatedDeveloperId { get; private set; }

    public string Id { get; private set; }

    public string DisplayName { get; private set; }

    public DevBoxMachineState DevBoxState { get; private set; }

    public DevBoxActionToPerform CurrentActionToPerform { get; private set; }

    public bool IsDevBoxBeingCreatedOrProvisioned => DevBoxState.ProvisioningState == Constants.DevBoxProvisioningStates.Creating || DevBoxState.ProvisioningState == Constants.DevBoxProvisioningStates.Provisioning;

    public DevBoxRemoteConnectionData? RemoteConnectionData { get; private set; }

    public IEnumerable<ComputeSystemProperty> Properties { get; set; } = new List<ComputeSystemProperty>();

    public event TypedEventHandler<IComputeSystem, ComputeSystemState>? StateChanged;

    public DevBoxInstance(
        IDevBoxAuthService devBoxAuthService,
        IDevBoxManagementService devBoxManagementService,
        IDevBoxOperationWatcher devBoxOperationWatcher,
        IDeveloperId developerId,
        DevBoxMachineState devBoxMachineState)
    {
        _authService = devBoxAuthService;
        _devBoxManagementService = devBoxManagementService;
        _devBoxOperationWatcher = devBoxOperationWatcher;

        DevBoxState = devBoxMachineState;
        AssociatedDeveloperId = developerId;
        DisplayName = devBoxMachineState.Name;
        Id = devBoxMachineState.UniqueId;

        if (IsDevBoxBeingCreatedOrProvisioned)
        {
            IsOperationInProgress = true;
        }

        ProcessProperties();
    }

    private void ProcessProperties()
    {
        if (!Properties.Any())
        {
            try
            {
                var properties = new List<ComputeSystemProperty>();
                var cpu = ComputeSystemProperty.Create(ComputeSystemPropertyKind.CpuCount, DevBoxState.HardwareProfile.VCPUs);
                var memory = ComputeSystemProperty.Create(ComputeSystemPropertyKind.AssignedMemorySizeInBytes, (ulong)DevBoxState.HardwareProfile.MemoryGB * Constants.BytesInGb);
                var diskSize = ComputeSystemProperty.Create(ComputeSystemPropertyKind.StorageSizeInBytes, (ulong)DevBoxState.StorageProfile.OsDisk.DiskSizeGB * Constants.BytesInGb);
                var operatingSystem = ComputeSystemProperty.CreateCustom(DevBoxState.ImageReference.OperatingSystem, Resources.GetResource(OsPropertyName), null);
                properties.Add(operatingSystem);
                properties.Add(cpu);
                properties.Add(memory);
                properties.Add(diskSize);
                Properties = properties;
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Error processing properties for {DisplayName}", ex);
            }
        }
    }

    private async Task GetRemoteLaunchURIsAsync(Uri boxURI)
    {
        var connectionUri = $"{boxURI}/remoteConnection?{Constants.APIVersion}";
        var result = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(connectionUri), AssociatedDeveloperId, HttpMethod.Get, null);
        RemoteConnectionData = JsonSerializer.Deserialize<DevBoxRemoteConnectionData>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
    }

    public ComputeSystemOperations SupportedOperations =>
        ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown | ComputeSystemOperations.Delete | ComputeSystemOperations.Restart;

    public string SupplementalDisplayName => $"{Resources.GetResource(SupplementalDisplayNamePrefix)}: {DevBoxState.ProjectName}";

    public string AssociatedProviderId => Constants.DevBoxProviderId;

    /// <summary>
    /// Common method to perform REST operations on the DevBox.
    /// This method is used by Start, ShutDown, Restart, and Delete.
    /// </summary>
    /// <param name="action">Operation to be performed</param>
    /// <param name="method">HttpMethod to be used</param>
    /// <returns>ComputeSystemOperationResult created from the result of the operation</returns>
    private IAsyncOperation<ComputeSystemOperationResult> PerformRESTOperation(DevBoxActionToPerform action, HttpMethod method)
    {
        return Task.Run(async () =>
        {
            try
            {
                lock (_operationLock)
                {
                    if (IsOperationInProgress)
                    {
                        return new ComputeSystemOperationResult(new InvalidOperationException(), Resources.GetResource(DevBoxMultipleConcurrentOperationsNotSupportedKey), "Running multiple operations is not supported");
                    }

                    IsOperationInProgress = true;
                }

                CurrentActionToPerform = action;
                var operation = DevBoxOperationHelper.ActionToPerformToString(action);

                // The creation and delete operations do not require an operation string, but all the other operations do.
                var operationUri = operation.Length > 0 ?
                    $"{DevBoxState.Uri}:{operation}?{Constants.APIVersion}" : $"{DevBoxState.Uri}?{Constants.APIVersion}";
                Log.Logger()?.ReportInfo($"Starting {DisplayName} with {operationUri}");

                var result = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(operationUri), AssociatedDeveloperId, method, null);
                var operationLocation = result.ResponseHeader.OperationLocation;

                // The last segment of the operationLocation is the operationId.
                // E.g https://8a40af38-3b4c-4672-a6a4-5e964b1870ed-contosodevcenter.centralus.devcenter.azure.com/projects/myProject/operationstatuses/786a823c-8037-48ab-89b8-8599901e67d0
                // See example Response: https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes/start-dev-box?view=rest-devcenter-developer-2023-04-01&tabs=HTTP
                var operationId = Guid.Parse(operationLocation!.Segments.Last());

                // Monitor the operations progress
                _devBoxOperationWatcher.StartDevCenterOperationMonitor(AssociatedDeveloperId, operationLocation!, operationId, action, OperationCallback);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                UpdateStateForUI();
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Unable to procress DevBox operation '{nameof(DevBoxOperation)}'", ex);
                return new ComputeSystemOperationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }

    private void RemoveOperationInProgressFlag()
    {
        lock (_operationLock)
        {
            IsOperationInProgress = false;
        }
    }

    /// <summary>
    /// When a Dev Box is created it the Dev Center started to provision it. We use this method once the provisioning is complete.
    /// </summary>
    /// <param name="devBoxMachineState">The new state of the Dev Box from the Dev Center</param>
    /// <param name="status">The status of the provisioing</param>
    public void ProvisioningMonitorCompleted(DevBoxMachineState? devBoxMachineState, ProvisioningStatus status)
    {
        if (!IsDevBoxBeingCreatedOrProvisioned)
        {
            return;
        }

        if (status == ProvisioningStatus.Succeeded && devBoxMachineState != null)
        {
            DevBoxState = devBoxMachineState;
        }
        else
        {
            // If the provisioning failed, we'll set the state to failed and powerstate to unknown.
            // The PowerState being unknown will make the UI show the Dev Box state as unknown.
            DevBoxState.ProvisioningState = Constants.DevBoxProvisioningStates.Failed;
            DevBoxState.PowerState = Constants.DevBoxPowerStates.Unknown;
        }

        RemoveOperationInProgressFlag();
        UpdateStateForUI();
    }

    /// <summary>
    /// Callback method for when we're performing a long running operation on the Dev Box. E.g. Start, Stop, Restart, Delete.
    /// </summary>
    /// <param name="status">The current status of the operation</param>
    public void OperationCallback(DevCenterOperationStatus? status)
    {
        switch (status)
        {
            case DevCenterOperationStatus.NotStarted:
            case DevCenterOperationStatus.Running:
                break;
            case DevCenterOperationStatus.Succeeded:
                SetStateAfterOperationCompletedSuccessfully();
                break;
            default:
                RemoveOperationInProgressFlag();
                UpdateStateForUI();
                Log.Logger()?.ReportError($"Dev Box operation stopped unexpectedly with status '{status}'");
                break;
        }
    }

    /// <summary>
    /// When a long running operation is complete, we'll update the state of the Dev Box. To do this we get
    /// the new state from the Dev Center.
    /// </summary>
    private void SetStateAfterOperationCompletedSuccessfully()
    {
        Task.Run(async () =>
        {
            try
            {
                if (CurrentActionToPerform == DevBoxActionToPerform.Delete)
                {
                    // DevBox no longer exists, so we can't get the state from the Dev Center.
                    // so we'll artificially set the power state to deleted.
                    DevBoxState.ProvisioningState = Constants.DevBoxDeletedState;
                }
                else
                {
                    // Refresh the state of the DevBox after the operation is complete.
                    var devBoxUri = new Uri($"{DevBoxState.Uri}?{Constants.APIVersion}");
                    var result = await _devBoxManagementService.HttpsRequestToDataPlane(devBoxUri, AssociatedDeveloperId, HttpMethod.Get, null);
                    DevBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;
                }

                RemoveOperationInProgressFlag();
                UpdateStateForUI();
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Error setting state after the long running operation completed successfully", ex);
                DevBoxState.ProvisioningState = Constants.DevBoxProvisioningStates.Failed;
                DevBoxState.PowerState = Constants.DevBoxPowerStates.Unknown;
                RemoveOperationInProgressFlag();
                UpdateStateForUI();
            }
        });
    }

    public void UpdateStateForUI()
    {
        StateChanged?.Invoke(this, GetState());
    }

    private bool IsTerminalProvisioningState(string provisioningState)
    {
        return provisioningState == Constants.DevBoxProvisioningStates.Succeeded
            || provisioningState == Constants.DevBoxProvisioningStates.ProvisionedWithWarning
            || provisioningState == Constants.DevBoxProvisioningStates.Canceled
            || provisioningState == Constants.DevBoxProvisioningStates.Failed;
    }

    private bool IsTerminalActionState(string actionState)
    {
        return actionState == Constants.DevBoxActionStates.Started
            || actionState == Constants.DevBoxActionStates.Stopped
            || actionState == Constants.DevBoxActionStates.Repaired
            || actionState == Constants.DevBoxActionStates.Unknown
            || actionState == Constants.DevBoxActionStates.Failed;
    }

    public ComputeSystemState GetState()
    {
        var provisioningState = DevBoxState.ProvisioningState;
        var actionState = DevBoxState.ActionState;
        var powerState = DevBoxState.PowerState;

        // This state is actually failed, but since ComputeSystemState doesn't have a failed state, we'll return unknown.
        if (provisioningState == Constants.DevBoxProvisioningStates.Failed
            || provisioningState == Constants.DevBoxProvisioningStates.ProvisionedWithWarning)
        {
            return ComputeSystemState.Unknown;
        }

        if (!IsTerminalProvisioningState(provisioningState))
        {
            switch (provisioningState)
            {
                case Constants.DevBoxProvisioningStates.Provisioning:
                case Constants.DevBoxProvisioningStates.Creating:
                    return ComputeSystemState.Creating;
                case Constants.DevBoxProvisioningStates.Deleting:
                    return ComputeSystemState.Deleting;
            }
        }

        if (!IsTerminalActionState(actionState))
        {
            switch (actionState)
            {
                case Constants.DevBoxActionStates.Starting:
                    return ComputeSystemState.Starting;
                case Constants.DevBoxActionStates.Stopping:
                    return ComputeSystemState.Stopping;
                case Constants.DevBoxActionStates.Restarting:
                    return ComputeSystemState.Restarting;

                // This state is actually repairing, but since ComputeSystemState doesn't have a repairing state, we'll return starting.
                case Constants.DevBoxActionStates.Repairing:
                    return ComputeSystemState.Starting;
            }
        }

        switch (powerState)
        {
            case Constants.DevBoxPowerStates.Running:
                return ComputeSystemState.Running;
            case Constants.DevBoxPowerStates.Hibernated:
                return ComputeSystemState.Paused;
            case Constants.DevBoxPowerStates.Stopped:
            case Constants.DevBoxPowerStates.Deallocated:
            case Constants.DevBoxPowerStates.PoweredOff:
                return ComputeSystemState.Stopped;
        }

        // This is a workaround from the web app for not getting power state for new VMs.
        switch (actionState)
        {
            case Constants.DevBoxActionStates.Unknown:
            case Constants.DevBoxActionStates.Stopped:
                return ComputeSystemState.Stopped;
            case Constants.DevBoxActionStates.Started:
                return ComputeSystemState.Running;
            case Constants.DevBoxActionStates.Failed:
                return ComputeSystemState.Unknown;
        }

        // If we reach this point, we are not sure of the state of the dev box so return unknown instead.
        return ComputeSystemState.Unknown;
    }

    public IAsyncOperation<ComputeSystemOperationResult> StartAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Starting);
        return PerformRESTOperation(DevBoxActionToPerform.Start, HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> ShutDownAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Stopping);
        return PerformRESTOperation(DevBoxActionToPerform.Stop, HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> RestartAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Restarting);
        return PerformRESTOperation(DevBoxActionToPerform.Restart, HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Deleting);
        return PerformRESTOperation(DevBoxActionToPerform.Delete, HttpMethod.Delete);
    }

    /// <summary>
    /// Launches the DevBox in a browser.
    /// </summary>
    /// <param name="options">Unused parameter</param>
    public IAsyncOperation<ComputeSystemOperationResult> ConnectAsync(string options)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (RemoteConnectionData is null)
                {
                    await GetRemoteLaunchURIsAsync(new Uri(DevBoxState.Uri));
                }

                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = RemoteConnectionData?.WebUrl;
                Process.Start(psi);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error connecting to {DisplayName}", ex);
                return new ComputeSystemOperationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> TerminateAsync(string options)
    {
        return ShutDownAsync(options);
    }

    public IAsyncOperation<ComputeSystemStateResult> GetStateAsync()
    {
        return Task.Run(() =>
        {
            return new ComputeSystemStateResult(GetState());
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemThumbnailResult> GetComputeSystemThumbnailAsync(string options)
    {
        return Task.Run(async () =>
        {
            try
            {
                var uri = new Uri(Constants.ThumbnailURI);
                var storageFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                var randomAccessStream = await storageFile.OpenReadAsync();

                // Convert the stream to a byte array
                var bytes = new byte[randomAccessStream.Size];

                // safely convert ulong to uint
                var maxSizeSafeUlongToUint = (uint)Math.Min(randomAccessStream.Size, uint.MaxValue);

                await randomAccessStream.ReadAsync(bytes.AsBuffer(), maxSizeSafeUlongToUint, InputStreamOptions.None);

                return new ComputeSystemThumbnailResult(bytes);
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error getting thumbnail for {DisplayName}", ex);
                return new ComputeSystemThumbnailResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<IEnumerable<ComputeSystemProperty>> GetComputeSystemPropertiesAsync(string options)
    {
        return Task.Run(() =>
        {
            return Properties;
        }).AsAsyncOperation();
    }

    // Unsupported operations
    public IAsyncOperation<ComputeSystemOperationResult> RevertSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> CreateSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> PauseAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ResumeAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> SaveAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ModifyPropertiesAsync(string inputJson)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), Resources.GetResource(Constants.DevBoxMethodNotImplementedKey), "Method not implemented");
        }).AsAsyncOperation();
    }

    // Apply configuration isn't supported yet for Dev Boxes. This functionality will be created before the feature is released at build.
    // Dev Home should not call this method and should use the Supported Operations to determine what operations are available.
    // Currently, the supported operations are Start, Shutdown, Restart, and Delete.
    public IApplyConfigurationOperation CreateApplyConfigurationOperation(string configuration) => throw new NotImplementedException();
}
