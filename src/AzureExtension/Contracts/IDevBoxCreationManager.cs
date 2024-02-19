// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AzureExtension.DevBox;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IDevBoxCreationManager
{
    public bool TryGetDevBoxInstanceIfBeingCreated(string id, out DevBoxInstance? devBox);

    /// <summary>
    /// Starts the operation to create a dev box.
    /// </summary>
    public Task StartCreateDevBoxOperation(CreateComputeSystemOperation operation, IDeveloperId developerId, string userOptions);

    /// <summary>
    /// Starts the process of monitoring a the provisioning status of the Dev Box. This is needed for the cases where a Dev Box was created 
    /// out side of the extension. But the creation operation is still in progress.
    /// </summary>
    public void StartDevBoxProvisioningStateMonitor(IDeveloperId developerId, DevBoxInstance devBox);
}
