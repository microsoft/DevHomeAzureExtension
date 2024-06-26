// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.System.Threading;
using DevBoxConstants = DevHomeAzureExtension.DevBox.Constants;

namespace DevHomeAzureExtension.Services.DevBox;

/// <summary>
/// Represents the status of a Dev Box provisioning operation.
/// </summary>
public enum ProvisioningStatus
{
    Succeeded,
    Failed,
}

/// <summary>
/// Watch service for Dev Box operations that are long running and asynchronous. This service is used to monitor the status of these operations
/// periodically as they are being performed in the Dev Center. We only watch for operations that are started by the extension. Or when
/// Dev Box is being provisioned.
/// </summary>
public class DevBoxOperationWatcher : IDevBoxOperationWatcher
{
    private readonly IDevBoxManagementService _managementService;

    private readonly ITimeSpanService _timeSpanService;

    private const string DevBoxOperationWatcherName = nameof(DevBoxManagementService);

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevBoxOperationWatcher));

    private readonly Dictionary<Guid, DevBoxOperationWatcherTimer> _operationWatcherTimers = new();

    private readonly object _lock = new();

    public DevBoxOperationWatcher(IDevBoxManagementService managementService, ITimeSpanService timeSpanService)
    {
        _managementService = managementService;
        _timeSpanService = timeSpanService;
    }

    /// <inheritdoc cref="IDevBoxOperationWatcher.StartDevCenterOperationMonitor"/>"
    /// <remarks>
    /// All Dev Box operations in the Dev Center are considered asynchronous and long running. This method is used to monitor the status of these operations.
    /// We use a ThreadPoolTimer to periodically query the Dev Center for the status of the operation. The interval of the timer is determined by the actionToPerform.
    /// The lambda expression passed to the timer is used to query the Dev Center for the status of the operation. If the operation is no longer running, the timer
    /// is stopped and the completionCallback is invoked. If the operation is still running, the timer continues to query the Dev Center for the status of the operation.
    /// While the timer is running, the operationId is used as the key in a dictionary to keep track of the timer. This is done so that we can cancel the timer once
    /// the operation is no longer running, at which point the operationId is removed from the dictionary. The operationId are guid values so they can be used as keys in the dictionary.
    /// </remarks>
    public void StartDevCenterOperationMonitor(IDeveloperId developerId, Uri operationUri, Guid operationId, DevBoxActionToPerform actionToPerform, Action<DevCenterOperationStatus?> completionCallback)
    {
        var interval = _timeSpanService.GetPeriodIntervalBasedOnAction(actionToPerform);

        // Only start the monitor if the Dev Box is not already being watched.
        if (!AddIdIfOperationNotBeingWatched(operationId))
        {
            return;
        }

        // Create periodic timer so that we can periodically query the Dev Center for the status of the operation.
        var timer = ThreadPoolTimer.CreatePeriodicTimer(
            async (ThreadPoolTimer timer) =>
            {
                try
                {
                    _log.Information($"Starting Dev Box operation with action {actionToPerform}, Uri: {operationUri}, Id: {operationId}");

                    // Query the Dev Center for the status of the Dev Box operation.
                    var result = await _managementService.HttpsRequestToDataPlane(operationUri, developerId, HttpMethod.Get, null);
                    var operation = JsonSerializer.Deserialize<DevCenterOperationBase>(result.JsonResponseRoot.ToString(), DevBoxConstants.JsonOptions)!;

                    // Invoke the call back so the original requester can handle the status update of the operation.
                    completionCallback(operation.Status);

                    // Check if the timer should be stopped
                    switch (operation.Status)
                    {
                        case DevCenterOperationStatus.NotStarted:
                        case DevCenterOperationStatus.Running:
                            break;
                        default:
                            _log.Information($"Dev Box operation monitoring stopped. {operation}, Uri: {operationUri}, Id: {operationId}");
                            RemoveTimerIfBeingWatched(operationId);
                            break;
                    }

                    ThrowIfTimerPastDeadline(operationId);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Dev Box operation monitoring stopped unexpectedly. Uri: {operationUri}, Id: {operationId}");

                    completionCallback(DevCenterOperationStatus.Failed);
                    RemoveTimerIfBeingWatched(operationId);
                }
            },
            interval);

        AddTimerToWatcherItem(operationId, timer);
    }

    /// See <inheritdoc cref="IDevBoxOperationWatcher.StartDevBoxProvisioningStatusMonitor"/>
    /// <remarks> This is a special case as there is no operationId associated with provisioning a Dev Box. If a Dev Box is created by the extension we receive a Uri to the location of
    /// the operation, so we can query it. We use <see cref="StartDevCenterOperationMonitor"/> in those cases. However if the Dev Box was created outside of the extension or if the user
    /// stops the extensions process or reboots the machine we will lose the Uri to the operation. In those cases we use this method to monitor the provisioning status of the Dev Box.
    /// When Dev Boxes are requested by Dev Home the <see cref="DevBoxMachineState"/> objects we get back from the Dev Center will have a provisioning state of "Provisioning". This indicates
    /// that the Dev Box is still being created. We use a ThreadPoolTimer to periodically query the Dev Center for the provisioning status of the Dev Box. Once this is set to
    /// "Succeeded" we know that the Dev Box has been created and we can stop the timer and invoke the completionCallback. DevBoxInstance Id's are GUIDs so we can use them as keys in for
    /// operationWaterTimers.
    /// </remarks>
    public void StartDevBoxProvisioningStatusMonitor(IDeveloperId developerId, DevBoxActionToPerform actionToPerform, DevBoxInstance devBoxInstance, Action<DevBoxInstance> completionCallback)
    {
        var interval = _timeSpanService.GetPeriodIntervalBasedOnAction(actionToPerform);
        var devBoxId = Guid.Parse(devBoxInstance.Id);

        // Only start the monitor if the Dev Box is not already being watched.
        if (!AddIdIfOperationNotBeingWatched(devBoxId))
        {
            return;
        }

        // Create periodic timer so that we can periodically query the Dev Center for the status of the operation.
        var timer = ThreadPoolTimer.CreatePeriodicTimer(
            async (ThreadPoolTimer timer) =>
            {
                try
                {
                    _log.Information($"Starting the provisioning monitor for Dev Box with Name: '{devBoxInstance.DisplayName}' , Id: '{devBoxInstance.Id}'");

                    // Query the Dev Center for the provisioning status of the Dev Box. This is needed for when the Dev Box was created outside of Dev Home.
                    var devBoxUri = $"{devBoxInstance.DevBoxState.Uri}?{DevBoxConstants.APIVersion}";
                    var result = await _managementService.HttpsRequestToDataPlane(new Uri(devBoxUri), developerId, HttpMethod.Get, null);
                    var devBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), DevBoxConstants.JsonOptions)!;

                    // If the Dev Box is no longer being created, update the state for Dev Homes UI and end the timer.
                    if (!(devBoxState.ProvisioningState == DevBoxConstants.DevBoxProvisioningStates.Creating || devBoxState.ProvisioningState == DevBoxConstants.DevBoxProvisioningStates.Provisioning))
                    {
                        _log.Information($"Dev Box provisioning now completed.");
                        devBoxInstance.ProvisioningMonitorCompleted(devBoxState, ProvisioningStatus.Succeeded);
                        completionCallback(devBoxInstance);
                        RemoveTimerIfBeingWatched(devBoxId);
                    }

                    ThrowIfTimerPastDeadline(devBoxId);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Dev Box provisioning monitoring stopped unexpectedly.");
                    devBoxInstance.ProvisioningMonitorCompleted(null, ProvisioningStatus.Failed);
                    completionCallback(devBoxInstance);
                    RemoveTimerIfBeingWatched(devBoxId);
                }
            },
            interval);

        AddTimerToWatcherItem(devBoxId, timer);
    }

    private void ThrowIfTimerPastDeadline(Guid id)
    {
        if (_operationWatcherTimers.TryGetValue(id, out var operationWatcherItem))
        {
            if (DateTime.Now - operationWatcherItem.StartTime > _timeSpanService.DevBoxOperationDeadline)
            {
                throw new DevBoxOperationMonitorException($"The operation has exceeded its '{_timeSpanService.DevBoxOperationDeadline.TotalHours} hour' deadline");
            }
        }
    }

    public void RemoveTimerIfBeingWatched(Guid id)
    {
        lock (_lock)
        {
            if (_operationWatcherTimers.TryGetValue(id, out var operationWatcherItem))
            {
                operationWatcherItem.Timer?.Cancel();
                _operationWatcherTimers.Remove(id);
            }
        }
    }

    /// <summary>
    /// Adds the id of the operation to the Dictionary only if it is currently not being watched.
    /// </summary>
    /// <param name="id">Id of the operation to associate with the timer</param>
    /// <returns>True only if the Id was added and false otherwise.</returns>
    public bool AddIdIfOperationNotBeingWatched(Guid id)
    {
        lock (_lock)
        {
            if (!_operationWatcherTimers.ContainsKey(id))
            {
                _operationWatcherTimers.Add(id, new DevBoxOperationWatcherTimer());
                return true;
            }

            return false;
        }
    }

    private void AddTimerToWatcherItem(Guid id, ThreadPoolTimer timer)
    {
        lock (_lock)
        {
            if (_operationWatcherTimers.TryGetValue(id, out var item) && item.Timer != null)
            {
                // If the timer is already set for this item cancel the new one and don't
                // add it to the watcher item.
                timer.Cancel();
                return;
            }

            _operationWatcherTimers[id].Timer = timer;
        }
    }

    public bool IsIdBeingWatched(Guid id)
    {
        lock (_lock)
        {
            return _operationWatcherTimers.ContainsKey(id);
        }
    }
}
