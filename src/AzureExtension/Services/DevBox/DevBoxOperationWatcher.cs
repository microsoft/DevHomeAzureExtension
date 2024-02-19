// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Exceptions;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Windows.System.Threading;

namespace AzureExtension.Services.DevBox;

public class DevBoxOperationWatcher : IDevBoxOperationWatcher
{
    private readonly IDevBoxManagementService _managementService;

    private readonly ITimeSpanService _timeSpanService;

    private const string DevBoxOperationWatcherName = nameof(DevBoxManagementService);

    private readonly Dictionary<string, DevBoxOperationWatcherItem> _operations = new();

    private readonly object _lock = new();

    public DevBoxOperationWatcher(IDevBoxManagementService managementService, ITimeSpanService timeSpanService)
    {
        _managementService = managementService;
        _timeSpanService = timeSpanService;
    }

    /// <inheritdoc cref="IDevBoxOperationWatcher.StartDevCenterOperationMonitor"/>"
    public void StartDevCenterOperationMonitor(
        object source,
        IDeveloperId developerId,
        Uri operationUri,
        string operationId,
        DevBoxActionToPerform actionToPerform,
        Action<DevCenterOperationStatus?> callback)
    {
        var interval = _timeSpanService.GetTimeSpanBasedOnAction(actionToPerform);

        lock (_lock)
        {
            if (_operations.ContainsKey(operationId))
            {
                return;
            }

            _operations.Add(operationId, new(source, actionToPerform, operationId, ThreadPoolTimer.CreatePeriodicTimer(
                async (ThreadPoolTimer timer) =>
                {
                    try
                    {
                        // Query the Dev Center for the status of the Dev Box operation.
                        var result = await _managementService.HttpRequestToDataPlane(operationUri, developerId, HttpMethod.Get, null);
                        var operation = JsonSerializer.Deserialize<DevCenterOperationBase>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;
                        callback(operation.Status);
                        switch (operation.Status)
                        {
                            case DevCenterOperationStatus.NotStarted:
                            case DevCenterOperationStatus.Running:
                                break;
                            default:
                                Log.Logger()?.ReportInfo(DevBoxOperationWatcherName, $"Dev Box operation monitoring stopped. {operation}, Uri: {operationUri}, Id: {operationId}");
                                RemoveTimer(operationId);
                                break;
                        }

                        ThrowIfTimerPastDeadline(operationId);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger()?.ReportError(DevBoxOperationWatcherName, $"Dev Box operation monitoring stopped unexpectedly. Uri: {operationUri}, Id: {operationId}", ex);

                        callback(DevCenterOperationStatus.Failed);
                        RemoveTimer(operationId);
                    }
                },
                interval)));
        }
    }

    /// See <inheritdoc cref="IDevBoxOperationWatcher.StartDevBoxProvisioningStatusMonitor"/>
    public void StartDevBoxProvisioningStatusMonitor(
        IDeveloperId developerId,
        DevBoxActionToPerform actionToPerform,
        DevBoxInstance devBoxInstance,
        Action<DevBoxInstance> callback)
    {
        var interval = _timeSpanService.GetTimeSpanBasedOnAction(actionToPerform);

        lock (_lock)
        {
            if (_operations.ContainsKey(devBoxInstance.Id))
            {
                return;
            }

            _operations.Add(devBoxInstance.Id, new(devBoxInstance, actionToPerform, devBoxInstance.Id, ThreadPoolTimer.CreatePeriodicTimer(
                async (ThreadPoolTimer timer) =>
                {
                    try
                    {
                        // Query the Dev Center for the provisioning status of the Dev Box. This is needed for when the Dev Box was created outside of Dev Home.
                        var devBoxUri = $"{devBoxInstance.DevBoxState.Uri}?{Constants.APIVersion}";
                        var result = await _managementService.HttpRequestToDataPlane(new Uri(devBoxUri), developerId, HttpMethod.Get, null);
                        var devBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;

                        // If the Dev Box is no longer being created, update the state for Dev Homes UI and end the timer.
                        if (!(devBoxState.ProvisioningState == Constants.DevBoxCreatingProvisioningState || devBoxState.ProvisioningState == Constants.DevBoxProvisioningState))
                        {
                            Log.Logger()?.ReportInfo(DevBoxOperationWatcherName, $"Dev Box provisioning now completed.");
                            devBoxInstance.ProvisioningMonitorCompleted(devBoxState, ProvisioningStatus.Succeeded);
                            callback(devBoxInstance);
                            RemoveTimer(devBoxInstance.Id);
                        }

                        ThrowIfTimerPastDeadline(devBoxInstance.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger()?.ReportError(DevBoxOperationWatcherName, $"Dev Box provisioning monitoring stopped unexpectedly.", ex);
                        devBoxInstance.ProvisioningMonitorCompleted(null, ProvisioningStatus.Failed);
                        callback(devBoxInstance);
                        RemoveTimer(devBoxInstance.Id);
                    }
                },
                interval)));
        }
    }

    private void ThrowIfTimerPastDeadline(string id)
    {
        lock (_lock)
        {
            if (_operations.TryGetValue(id, out var operationWatcherItem))
            {
                if (DateTime.Now - operationWatcherItem.StartTime > _timeSpanService.DevBoxOperationDeadline)
                {
                    throw new DevBoxOperationMonitorException($"The operation has exceeded its '{_timeSpanService.DevBoxOperationDeadline.TotalHours} hour' deadline");
                }
            }
        }
    }

    private void RemoveTimer(string id)
    {
        lock (_lock)
        {
            if (_operations.TryGetValue(id, out var operationWatcherItem))
            {
                operationWatcherItem.Timer?.Cancel();
                _operations.Remove(id);
            }
        }
    }

    public bool IsOperationCurrentlyBeingWatched(string id)
    {
        lock (_lock)
        {
            return _operations.ContainsKey(id);
        }
    }

    public bool TryGetOperationSourceObject(string id, out object? source)
    {
        source = null;

        lock (_lock)
        {
            if (_operations.TryGetValue(id, out var value))
            {
                source = value.Source;
                return true;
            }

            return false;
        }
    }
}
