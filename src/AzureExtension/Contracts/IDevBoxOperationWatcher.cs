// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IDevBoxOperationWatcher
{
    /// <summary>
    /// Method used to start a long running operation monitor for a Dev Center operation.
    /// </summary>
    public void StartDevCenterOperationMonitor(object source, IDeveloperId developerId, Uri operationUri, string operationId, DevBoxActionToPerform actionToPerform, Action<DevCenterOperationStatus?> callback);

    /// <summary>
    /// Method used to monitor the provisioning status of a Dev Box that was created outside of the extension.
    /// </summary>
    public void StartDevBoxProvisioningStatusMonitor(IDeveloperId developerId, DevBoxActionToPerform actionToPerform, DevBoxInstance devBoxInstance, Action<DevBoxInstance> callback);

    public bool TryGetOperationSourceObject(string id, out object? source);

    public bool IsOperationCurrentlyBeingWatched(string id);
}
