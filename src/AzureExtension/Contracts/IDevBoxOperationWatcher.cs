// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

/// <summary>
/// Inteface used to watch Dev Box operations asynchronously that take place in the Dev Center.
/// </summary>
public interface IDevBoxOperationWatcher
{
    /// <summary>
    /// Method used to start a long running operation monitor for a Dev Center operation.
    /// </summary>
    /// <param name="developerId">The developer to authenticate with</param>
    /// <param name="operationUri">The Uri associated with the operation. It is the Uri that the operation will continuously query</param>
    /// <param name="operationId">The Id of the operation provided by the Dev Center</param>
    /// <param name="actionToPerform">The DevBox action assocated with the operation</param>
    /// <param name="completionCallback">A callback  operation to be invoked once the operation completes</param>
    public void StartDevCenterOperationMonitor(IDeveloperId developerId, Uri operationUri, Guid operationId, DevBoxActionToPerform actionToPerform, Action<DevCenterOperationStatus?> completionCallback);

    /// <summary>
    /// Method used to monitor the provisioning status of a Dev Box that was created outside of the extension.
    /// </summary>
    /// <param name="developerId">The developer to authenticate with</param>
    /// <param name="actionToPerform">The DevBox action assocated with the operation</param>
    /// <param name="devBoxInstance">The DevBox instance whose provisioning status is being watched.</param>
    /// <param name="completionCallback">A callback  operation to be invoked once the operation completes</param>
    public void StartDevBoxProvisioningStatusMonitor(IDeveloperId developerId, DevBoxActionToPerform actionToPerform, DevBoxInstance devBoxInstance, Action<DevBoxInstance> completionCallback);
}
