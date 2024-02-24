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
    /// Initiates the Dev box creation process. The operation is started, and a request is made to the Dev Center who will
    /// then create the Dev Box.
    /// </summary>
    /// <param name="operation">An object that implements Dev Homes <see cref="ICreateComputeSystemOperation"/> interface to relay progress data back to Dev Home</param>
    /// <param name="developerId">The developer to authenticate with</param>
    /// <param name="parameters">User json input provided from Dev Home</param>
    public Task StartCreateDevBoxOperation(CreateComputeSystemOperation operation, IDeveloperId developerId, DevBoxCreationParameters parameters);

    /// <summary>
    /// Starts the process of monitoring the provisioning status of the Dev Box. This is needed for the cases where a Dev Box was created
    /// out side of the extension. But the creation operation is still in progress.
    /// </summary>
    /// <param name="developerId">The developer to authenticate with</param>
    /// <param name="devBox">The Dev Box instance to monitor</param>
    public void StartDevBoxProvisioningStateMonitor(IDeveloperId developerId, DevBoxInstance devBox);
}
