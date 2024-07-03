// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Services.DevBox;
using Windows.System.Threading;

namespace DevHomeAzureExtension.DevBox.Models;

/// <summary>
/// Used to store information about a timer that is being stored by the <see cref="DevBoxOperationWatcher"/>.
/// </summary>
public class DevBoxOperationWatcherTimer
{
    /// <summary>
    /// Gets or sets the thread pool timer associated with the object.
    /// </summary>
    public ThreadPoolTimer? Timer { get; set; }

    /// <summary>
    /// Gets the start time of the operation
    /// </summary>
    public DateTime StartTime { get; private set; } = DateTime.Now;
}
