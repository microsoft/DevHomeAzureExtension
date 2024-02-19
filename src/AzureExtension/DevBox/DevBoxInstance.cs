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
using static System.Windows.Forms.AxHost;

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

    private readonly object _operationLock = new();

    public bool IsOperationInProgress { get; private set; }

    public IDeveloperId AssociatedDeveloperId { get; private set; }

    public string Id { get; private set; }

    public string Name { get; private set; }

    public DevBoxMachineState DevBoxState { get; private set; }

    public DevBoxActionToPerform CurrentActionToPerform { get; private set; }

    public bool IsDevBoxBeingCreatedOrProvisioned => DevBoxState.ProvisioningState == Constants.DevBoxCreatingProvisioningState || DevBoxState.ProvisioningState == Constants.DevBoxProvisioningState;

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
        Name = devBoxMachineState.Name;
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
                var cpu = new ComputeSystemProperty(DevBoxState.HardwareProfile.VCPUs, ComputeSystemPropertyKind.CpuCount);
                var memory = new ComputeSystemProperty((ulong)DevBoxState.HardwareProfile.MemoryGB * Constants.BytesInGb, ComputeSystemPropertyKind.AssignedMemorySizeInBytes);
                var diskSize = new ComputeSystemProperty((ulong)DevBoxState.StorageProfile.OsDisk.DiskSizeGB * Constants.BytesInGb, ComputeSystemPropertyKind.StorageSizeInBytes);
                var operatingSystem = new ComputeSystemProperty(Resources.GetResource(OsPropertyName), DevBoxState.ImageReference.OperatingSystem, ComputeSystemPropertyKind.Generic);
                properties.Add(operatingSystem);
                properties.Add(cpu);
                properties.Add(memory);
                properties.Add(diskSize);

                Properties = properties;
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Error processing properties for {Name}", ex);
            }
        }
    }

    private async Task GetRemoteLaunchURIsAsync(Uri boxURI)
    {
        var connectionUri = $"{boxURI}/remoteConnection?{Constants.APIVersion}";
        var result = await _devBoxManagementService.HttpRequestToDataPlane(new Uri(connectionUri), AssociatedDeveloperId, HttpMethod.Get, null);
        RemoteConnectionData = JsonSerializer.Deserialize<DevBoxRemoteConnectionData>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
    }

    public ComputeSystemOperations SupportedOperations =>
        ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown | ComputeSystemOperations.Delete | ComputeSystemOperations.Restart | ComputeSystemOperations.ApplyConfiguration;

    public string AlternativeDisplayName => $"{Resources.GetResource(SupplementalDisplayNamePrefix)}: {DevBoxState.ProjectName}";

    public string AssociatedProviderId => Constants.DevBoxProviderName;

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
                        return new ComputeSystemOperationResult(new InvalidOperationException(), "Running multiple operations is not supported");
                    }

                    IsOperationInProgress = true;
                }

                CurrentActionToPerform = action;
                var operation = DevBoxOperationHelper.ActionToPerformToString(action);

                // The creation and delete operations do not require an operation string, but all the other operations do.
                var operationUri = operation.Length > 0 ?
                    $"{DevBoxState.Uri}:{operation}?{Constants.APIVersion}" : $"{DevBoxState.Uri}?{Constants.APIVersion}";
                Log.Logger()?.ReportInfo($"Starting {Name} with {operationUri}");

                var result = await _devBoxManagementService.HttpRequestToDataPlane(new Uri(operationUri), AssociatedDeveloperId, method, null);
                var uri = result.ResponseHeader.OperationLocation;
                var operationid = result.ResponseHeader.Id;

                // Monitor the operations progress every minute.
                _devBoxOperationWatcher.StartDevCenterOperationMonitor(this, AssociatedDeveloperId, uri!, operationid, action, OperationCallback);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                UpdateStateForUI();
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Unable to procress DevBox operation '{nameof(DevBoxOperation)}'", ex);
                return new ComputeSystemOperationResult(ex, ex.Message);
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
            DevBoxState.ProvisioningState = Constants.DevBoxProvisioningFailedState;
            DevBoxState.PowerState = Constants.DevBoxUnknownState;
        }

        RemoveOperationInProgressFlag();
        UpdateStateForUI();
    }

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

    private void SetStateAfterOperationCompletedSuccessfully()
    {
        Task.Run(async () =>
        {
            try
            {
                if (CurrentActionToPerform == DevBoxActionToPerform.Delete)
                {
                    // DevBox no longer exists, so we can't get the state from the DevBox.
                    // so we'll artificially set the power state to deleted.
                    DevBoxState.PowerState = Constants.DevBoxDeletedState;
                }
                else
                {
                    // Refresh the state of the DevBox after the operation is complete.
                    var devBoxUri = new Uri($"{DevBoxState.Uri}?{Constants.APIVersion}");
                    var result = await _devBoxManagementService.HttpRequestToDataPlane(devBoxUri, AssociatedDeveloperId, HttpMethod.Get, null);
                    DevBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;
                }

                RemoveOperationInProgressFlag();
                UpdateStateForUI();
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError(DevBoxInstanceName, $"Error setting state after the long running operation completed successfully", ex);
                DevBoxState.ProvisioningState = Constants.DevBoxProvisioningFailedState;
                DevBoxState.PowerState = Constants.DevBoxUnknownState;
                RemoveOperationInProgressFlag();
                UpdateStateForUI();
            }
        });
    }

    public void UpdateStateForUI()
    {
        StateChanged?.Invoke(this, GetState());
    }

    public ComputeSystemState GetState()
    {
        switch (DevBoxState.ProvisioningState)
        {
            case Constants.DevBoxCreatingProvisioningState:
            case Constants.DevBoxProvisioningState:
                return ComputeSystemState.Creating;

            // Fallthrough if we're not creating or provisioning and look at the power state.
        }

        switch (DevBoxState.PowerState)
        {
            case Constants.DevBoxRunningState:
                return ComputeSystemState.Running;
            case Constants.DevBoxDeletedState:
                return ComputeSystemState.Deleted;

            // We don't have a direct mapping for the hiberbated state and may need to add one one day.
            case Constants.DevBoxHibernatedState:
            case Constants.DevBoxDeallocatedState:
            case Constants.DevBoxPoweredOffState:
                return ComputeSystemState.Stopped;
            default:
                return ComputeSystemState.Unknown;
        }
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
                Log.Logger()?.ReportError($"Error connecting to {Name}", ex);
                return new ComputeSystemOperationResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> TerminateAsync(string options)
    {
        return ShutDownAsync(options);
    }

    public IAsyncOperation<ComputeSystemStateResult> GetStateAsync(string options)
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
                await randomAccessStream.ReadAsync(bytes.AsBuffer(), (uint)randomAccessStream.Size, InputStreamOptions.None);

                return new ComputeSystemThumbnailResult(bytes);
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error getting thumbnail for {Name}: {ex.ToString}");
                return new ComputeSystemThumbnailResult(ex, string.Empty);
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
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> CreateSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> PauseAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ResumeAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> SaveAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ModifyPropertiesAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IApplyConfigurationOperation ApplyConfiguration(string configuration) => throw new NotImplementedException();
}
