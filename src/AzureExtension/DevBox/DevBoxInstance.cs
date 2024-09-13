// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Web;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Helpers;
using DevHomeAzureExtension.DevBox.Models;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Services.DevBox;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace DevHomeAzureExtension.DevBox;

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
public class DevBoxInstance : IComputeSystem, IComputeSystem2
{
    [DllImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const string WindowsAppEventName = "Global\\WA_SET_FOREGROUND";

    // the time to wait in milliseconds for the DevHomeAzureExtension process to create its window
    private const int PinningWaitForInputIdleTime = 2000;

    private readonly IDevBoxManagementService _devBoxManagementService;

    private readonly IDevBoxOperationWatcher _devBoxOperationWatcher;

    private readonly IPackagesService _packagesService;

    private readonly ILogger _log;

    private const string SupplementalDisplayNamePrefix = "DevBox_SupplementalDisplayNamePrefix";

    private const string OsPropertyName = "DevBox_OsDisplayText";

    private const string DevBoxInstanceName = "DevBoxInstance";

    private const string DevBoxMultipleConcurrentOperationsNotSupportedKey = "DevBox_MultipleConcurrentOperationsNotSupport";

    private static readonly CompositeFormat _protocolPinString = CompositeFormat.Parse("ms-cloudpc:pin?location={0}&request={1}&cpcid={2}&workspaceName={3}&environment={4}&username={5}&version=0.0&source=DevHome");

    // This is the version of the Windows App package that supports protocol associations for pinning
    private static readonly PackageVersion _minimumWindowsAppVersion = new(1, 3, 243, 0);

    // These exit codes must be kept in sync with WindowsApp
    private const int ExitCodeInvalid = -1;

    private const int ExitCodeFailure = 0;

    private const int ExitCodeSuccess = 1;

    private const int ExitCodePinned = 2;

    private const int ExitCodeUnpinned = 3;

    private readonly object _operationLock = new();

    public bool IsOperationInProgress { get; private set; }

    public IDeveloperId AssociatedDeveloperId { get; private set; }

    public string Id { get; private set; }

    public string DisplayName { get; private set; }

    public string? WorkspaceId { get; private set; }

    public string? Username { get; private set; }

    public string? Environment { get; private set; }

    public DevBoxMachineState DevBoxState { get; private set; }

    public DevBoxActionToPerform CurrentActionToPerform { get; private set; }

    public bool IsDevBoxBeingCreatedOrProvisioned => DevBoxState.ProvisioningState == Constants.DevBoxProvisioningStates.Creating || DevBoxState.ProvisioningState == Constants.DevBoxProvisioningStates.Provisioning;

    public DevBoxRemoteConnectionData? RemoteConnectionData { get; private set; }

    public IEnumerable<ComputeSystemProperty> Properties { get; set; } = new List<ComputeSystemProperty>();

    public event TypedEventHandler<IComputeSystem, ComputeSystemState>? StateChanged;

    public DevBoxInstance(
        IDevBoxManagementService devBoxManagementService,
        IDevBoxOperationWatcher devBoxOperationWatcher,
        IDeveloperId developerId,
        DevBoxMachineState devBoxMachineState,
        IPackagesService packagesService)
    {
        _devBoxManagementService = devBoxManagementService;
        _devBoxOperationWatcher = devBoxOperationWatcher;
        _packagesService = packagesService;

        DevBoxState = devBoxMachineState;
        AssociatedDeveloperId = developerId;
        DisplayName = devBoxMachineState.Name;
        Id = devBoxMachineState.UniqueId;
        _log = Log.ForContext("SourceContext", $"DevBox/{Id}");

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
                _log.Information($"Retrieving properties for Dev Box '{DisplayName}'");
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
                _log.Error(ex, $"Error processing properties for '{DisplayName}'");
            }
        }
    }

    private async Task GetRemoteLaunchURIsAsync(Uri boxURI)
    {
        var connectionUri = $"{boxURI}/remoteConnection?{Constants.APIVersion}";
        var result = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(connectionUri), AssociatedDeveloperId, HttpMethod.Get, null);
        RemoteConnectionData = JsonSerializer.Deserialize<DevBoxRemoteConnectionData>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
    }

    public ComputeSystemOperations SupportedOperations => GetOperations();

    // Is lversion greater than rversion
    public bool IsPackageVersionGreaterThan(PackageVersion lversion, PackageVersion rversion)
    {
        if (lversion.Major != rversion.Major)
        {
            return lversion.Major > rversion.Major;
        }

        if (lversion.Minor != rversion.Minor)
        {
            return lversion.Minor > rversion.Minor;
        }

        if (lversion.Build != rversion.Build)
        {
            return lversion.Build > rversion.Build;
        }

        if (lversion.Revision != rversion.Revision)
        {
            return lversion.Revision > rversion.Revision;
        }

        return false;
    }

    private ComputeSystemOperations GetOperations()
    {
        var state = GetState();
        if ((state == ComputeSystemState.Creating) || (CurrentActionToPerform != DevBoxActionToPerform.None))
        {
            return ComputeSystemOperations.Delete;
        }

        var operations = ComputeSystemOperations.Delete;

        switch (state)
        {
            // Dev Box in process of being deleted
            case ComputeSystemState.Deleting:
            case ComputeSystemState.Deleted:
                return ComputeSystemOperations.None;
            case ComputeSystemState.Saved:
                // Dev Box in hibernation
                operations |= ComputeSystemOperations.Resume |
                    ComputeSystemOperations.ShutDown |
                    ComputeSystemOperations.ApplyConfiguration |
                    ComputeSystemOperations.Restart;
                break;
            case ComputeSystemState.Running:
                // Dev Box is running
                operations |= ComputeSystemOperations.ShutDown |
                    ComputeSystemOperations.ApplyConfiguration |
                    ComputeSystemOperations.Restart;
                break;
            case ComputeSystemState.Stopped:
                // Dev Box is stopped
                operations |= ComputeSystemOperations.Start |
                    ComputeSystemOperations.ApplyConfiguration;
                break;
            default:
                // All other states should return the delete operation only
                return ComputeSystemOperations.Delete;
        }

        if (_packagesService.IsPackageInstalled(Constants.WindowsAppPackageFamilyName))
        {
            PackageVersion version = _packagesService.GetPackageInstalledVersion(Constants.WindowsAppPackageFamilyName);
            if (IsPackageVersionGreaterThan(version, _minimumWindowsAppVersion))
            {
                operations |= ComputeSystemOperations.PinToStartMenu | ComputeSystemOperations.PinToTaskbar;
            }
        }

        return operations;
    }

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
                    // Only the delete operation can be performed while other operations are being performed.
                    if (IsOperationInProgress &&
                        (CurrentActionToPerform != DevBoxActionToPerform.Delete) &&
                        (action != DevBoxActionToPerform.Delete))
                    {
                        _log.Error("Multiple operations are not supported for DevBoxes. Only the Delete operation can be performed while other operations are in progress");
                        return new ComputeSystemOperationResult(new InvalidOperationException(), Resources.GetResource(DevBoxMultipleConcurrentOperationsNotSupportedKey), "Running multiple operations is not supported");
                    }

                    IsOperationInProgress = true;
                }

                // Remove any operation that's being watched by the operation watcher now that the user has chosen to delete
                // the Dev Box.
                if (action == DevBoxActionToPerform.Delete)
                {
                    _devBoxOperationWatcher.RemoveTimerIfBeingWatched(Guid.Parse(Id));
                }

                CurrentActionToPerform = action;
                var operation = DevBoxOperationHelper.ActionToPerformToString(action);

                // The creation and delete operations do not require an operation string, but all the other operations do.
                var operationUri = operation.Length > 0 ?
                    $"{DevBoxState.Uri}:{operation}?{Constants.APIVersion}" : $"{DevBoxState.Uri}?{Constants.APIVersion}";

                _log.Information($"Performing {operation} operation for '{DisplayName}' with {operationUri}");

                var result = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(operationUri), AssociatedDeveloperId, method, null);
                var operationLocation = result.ResponseHeader.OperationLocation;

                // The last segment of the operationLocation is the operationId.
                // E.g https://8a40af38-3b4c-4672-a6a4-5e964b1870ed-contosodevcenter.centralus.devcenter.azure.com/projects/myProject/operationstatuses/786a823c-8037-48ab-89b8-8599901e67d0
                // See example Response: https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes/start-dev-box?view=rest-devcenter-developer-2023-04-01&tabs=HTTP
                var operationId = Guid.Parse(operationLocation!.Segments.Last());

                _log.Information($"Adding Dev Box '{DisplayName}' with to OperationMonitor");

                // Monitor the operations progress
                _devBoxOperationWatcher.StartDevCenterOperationMonitor(AssociatedDeveloperId, operationLocation!, operationId, action, OperationCallback);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                UpdateStateForUI();
                _log.Error(ex, $"Unable to process DevBox operation '{nameof(DevBoxOperation)}'");
                return new ComputeSystemOperationResult(ex, Constants.OperationsDefaultErrorMsg, ex.Message);
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
    /// <param name="status">The status of the provisioning</param>
    public void ProvisioningMonitorCompleted(DevBoxMachineState? devBoxMachineState, ProvisioningStatus status)
    {
        if (!IsDevBoxBeingCreatedOrProvisioned)
        {
            return;
        }

        if (status == ProvisioningStatus.Succeeded && devBoxMachineState != null)
        {
            _log.Information($"Dev Box provisioning succeeded for '{DisplayName}'");
            DevBoxState = devBoxMachineState;
        }
        else
        {
            _log.Information($"Dev Box provisioning failed for '{DisplayName}'");

            // If the provisioning failed, we'll set the state to failed and power state to unknown.
            // The PowerState being unknown will make the UI show the Dev Box state as unknown.
            DevBoxState.ProvisioningState = Constants.DevBoxProvisioningStates.Failed;
            DevBoxState.PowerState = Constants.DevBoxPowerStates.Unknown;
        }

        RemoveOperationInProgressFlag();
        UpdateStateForUI();
        CurrentActionToPerform = DevBoxActionToPerform.None;
    }

    /// <summary>
    /// Callback method for when we're performing a long running operation on the Dev Box. E.g. Start, Stop, Restart, Delete.
    /// </summary>
    /// <param name="status">The current status of the operation</param>
    public void OperationCallback(DevCenterOperationStatus? status)
    {
        _log.Information($"Dev Box operation Callback for '{DisplayName}' invoked with status '{status}'");
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
                CurrentActionToPerform = DevBoxActionToPerform.None;
                _log.Error($"Dev Box operation stopped unexpectedly with status '{status}'");
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
                _log.Information($"Dev Box operation completed for '{DisplayName}'. ActionPerformed: '{CurrentActionToPerform}'");
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
                _log.Error(ex, $"Error setting state after the long running operation completed successfully");
                DevBoxState.ProvisioningState = Constants.DevBoxProvisioningStates.Failed;
                DevBoxState.PowerState = Constants.DevBoxPowerStates.Unknown;
                RemoveOperationInProgressFlag();
                UpdateStateForUI();
            }

            CurrentActionToPerform = DevBoxActionToPerform.None;
        });
    }

    public void UpdateStateForUI()
    {
        try
        {
            StateChanged?.Invoke(this, GetState());
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Unable to invoke state changed event for {DisplayName}");
        }
    }

    // Check if it is a final provisioning state
    private bool IsTerminalProvisioningState(string provisioningState)
    {
        return provisioningState == Constants.DevBoxProvisioningStates.Succeeded
            || provisioningState == Constants.DevBoxProvisioningStates.ProvisionedWithWarning
            || provisioningState == Constants.DevBoxProvisioningStates.Canceled
            || provisioningState == Constants.DevBoxProvisioningStates.Failed;
    }

    // Check if it is a final action state
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

        _log.Information($"Getting State for devBox: '{DisplayName}', Provisioning: {provisioningState}, Action: {actionState}, Power: {powerState}");

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
                case Constants.DevBoxProvisioningStates.Deleted:
                    return ComputeSystemState.Deleted;
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
                return ComputeSystemState.Saved;
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
                _log.Information($"Retrieving remote connection data for '{DisplayName}'");
                if (RemoteConnectionData is null)
                {
                    await GetRemoteLaunchURIsAsync(new Uri(DevBoxState.Uri));
                }

                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                var isWindowsAppInstalled = _packagesService.IsPackageInstalled(Constants.WindowsAppPackageFamilyName);
                psi.FileName = isWindowsAppInstalled ? RemoteConnectionData?.CloudPcConnectionUrl : RemoteConnectionData?.WebUrl;

                _log.Information($"Launching DevBox '{DisplayName}' with connection data: {psi.FileName}. Windows App installed: {(isWindowsAppInstalled ? "True" : "False")}");

                Process.Start(psi);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error connecting to '{DisplayName}'");
                return new ComputeSystemOperationResult(ex, Constants.OperationsDefaultErrorMsg, string.Empty);
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
        return Task.Run(() =>
        {
            try
            {
                _log.Information($"Retrieving thumbnail for '{DisplayName}'");
                return new ComputeSystemThumbnailResult(Constants.ThumbnailInBytes.Value);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error getting thumbnail for '{DisplayName}'");
                return new ComputeSystemThumbnailResult(ex, Constants.OperationsDefaultErrorMsg, ex.Message);
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

    public IApplyConfigurationOperation CreateApplyConfigurationOperation(string configuration)
    {
        return new WingetConfigWrapper(this, configuration, _devBoxManagementService, _log);
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
        StateChanged?.Invoke(this, ComputeSystemState.Starting);
        return PerformRESTOperation(DevBoxActionToPerform.Start, HttpMethod.Post);
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

    private string ValidateWindowsAppAndItsParameters()
    {
        var isWindowsAppInstalled = _packagesService.IsPackageInstalled(Constants.WindowsAppPackageFamilyName);

        if (!isWindowsAppInstalled)
        {
            return "Windows App is not installed on the system";
        }

        PackageVersion version = _packagesService.GetPackageInstalledVersion(Constants.WindowsAppPackageFamilyName);

        if (!IsPackageVersionGreaterThan(version, _minimumWindowsAppVersion))
        {
            return "Older version of Windows App installed on the system";
        }
        else if (string.IsNullOrEmpty(WorkspaceId) || string.IsNullOrEmpty(DisplayName) || string.IsNullOrEmpty(Environment) || string.IsNullOrEmpty(Username))
        {
            return $"ValidateWindowsAppParameters failed with workspaceid={WorkspaceId} displayname={DisplayName} environment={Environment} username={Username}";
        }
        else
        {
            return string.Empty;
        }
    }

    public IAsyncOperation<ComputeSystemOperationResult> DoPinActionAsync(string location, string pinAction)
    {
        return Task.Run(() =>
        {
            try
            {
                var errorString = ValidateWindowsAppAndItsParameters();
                if (!string.IsNullOrEmpty(errorString))
                {
                    _log.Error(errorString);
                    throw new InvalidDataException(errorString);
                }

                var exitCode = ExitCodeInvalid;
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = string.Format(CultureInfo.InvariantCulture, _protocolPinString, location, pinAction, WorkspaceId, DisplayName, Environment, Username);

                Process? process = Process.Start(psi);
                if (process != null)
                {
                    process.Refresh();
                    AllowSetForegroundWindow(process.Id);

                    // This signals to the WindowsApp that it has been given foreground rights
                    var signalForegroundSet = new EventWaitHandle(false, EventResetMode.AutoReset, WindowsAppEventName);
                    signalForegroundSet.Set();

                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    if (exitCode == ExitCodeSuccess)
                    {
                        UpdateStateForUI();
                        return new ComputeSystemOperationResult();
                    }
                }

                errorString = $"DoPinActionAsync with location {location} and action {pinAction} failed with exitCode: {exitCode}";
                throw new NotSupportedException(errorString);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unable to perform the pinning action for DevBox: {DisplayName}");
                UpdateStateForUI();
                return new ComputeSystemOperationResult(ex, Constants.OperationsDefaultErrorMsg, "Pinning action failed");
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> PinToStartMenuAsync()
    {
        // Since there is no 'Pinning' state we'll say that the state is saving
        StateChanged?.Invoke(this, ComputeSystemState.Saving);
        return DoPinActionAsync("startMenu", "pin");
    }

    public IAsyncOperation<ComputeSystemOperationResult> UnpinFromStartMenuAsync()
    {
        // Since there is no 'Unpinning' state we'll say that the state is saving
        StateChanged?.Invoke(this, ComputeSystemState.Saving);
        return DoPinActionAsync("startMenu", "unpin");
    }

    public IAsyncOperation<ComputeSystemOperationResult> PinToTaskbarAsync()
    {
        // Since there is no 'Pinning' state we'll say that the state is saving
        StateChanged?.Invoke(this, ComputeSystemState.Saving);
        return DoPinActionAsync("taskbar", "pin");
    }

    public IAsyncOperation<ComputeSystemOperationResult> UnpinFromTaskbarAsync()
    {
        // Since there is no 'Unpinning' state we'll say that the state is saving
        StateChanged?.Invoke(this, ComputeSystemState.Saving);
        return DoPinActionAsync("taskbar", "unpin");
    }

    public IAsyncOperation<ComputeSystemPinnedResult> GetPinStatusAsync(string location, string errorMessage)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationString = ValidateWindowsAppAndItsParameters();
                if (!string.IsNullOrEmpty(validationString))
                {
                    throw new NotSupportedException(validationString);
                }

                var exitcode = ExitCodeInvalid;
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = string.Format(CultureInfo.InvariantCulture, _protocolPinString, location, "status", WorkspaceId, DisplayName, Environment, Username);
                Process? process = Process.Start(psi);
                if (process != null)
                {
                    process.Refresh();
                    AllowSetForegroundWindow(process.Id);

                    // This signals to the WindowsApp that it has been given foreground rights
                    var signalForegroundSet = new EventWaitHandle(false, EventResetMode.AutoReset, WindowsAppEventName);
                    signalForegroundSet.Set();

                    process.WaitForExit();
                    exitcode = process.ExitCode;
                    if (exitcode == ExitCodePinned)
                    {
                        return new ComputeSystemPinnedResult(true);
                    }
                    else if (exitcode == ExitCodeUnpinned)
                    {
                        return new ComputeSystemPinnedResult(false);
                    }
                }

                var errorString = $"GetPinStatusAsync from location {location} failed with exitcode: {exitcode}";
                throw new NotSupportedException(errorString);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unable to get pinning status for DevBox: {DisplayName}");
                return new ComputeSystemPinnedResult(ex, errorMessage, "Pinning status check failed");
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemPinnedResult> GetIsPinnedToStartMenuAsync()
    {
        return GetPinStatusAsync("startmenu", Constants.StartMenuPinnedStatusErrorMsg);
    }

    public IAsyncOperation<ComputeSystemPinnedResult> GetIsPinnedToTaskbarAsync()
    {
        return GetPinStatusAsync("taskbar", Constants.TaskbarPinnedStatusErrorMsg);
    }

    public async Task LoadWindowsAppParameters()
    {
        try
        {
            if (RemoteConnectionData is null)
            {
                await GetRemoteLaunchURIsAsync(new Uri(DevBoxState.Uri));
            }

            var uriString = RemoteConnectionData?.CloudPcConnectionUrl;
            if (string.IsNullOrEmpty(uriString))
            {
                _log.Information($"CloudPcConnectionUrl was empty of null or empty for Dev Box: '{DisplayName}'");
                return;
            }

            var launchUri = new Uri(uriString);
            WorkspaceId = HttpUtility.ParseQueryString(launchUri.Query)["cpcid"];
            Username = HttpUtility.ParseQueryString(launchUri.Query)["username"];
            Environment = HttpUtility.ParseQueryString(launchUri.Query)["environment"];
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"LoadWindowsAppParameters failed");
        }
    }
}
