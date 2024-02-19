// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System.Threading;

namespace AzureExtension.DevBox.Models;

public class DevBoxOperationWatcherItem
{
    public DevBoxActionToPerform ActionToPerform { get; set; }

    public string Id { get; set; } = string.Empty;

    public ThreadPoolTimer? Timer { get; set; }

    public DateTime StartTime { get; set; } = DateTime.Now;

    public object? Source { get; set; }

    public DevBoxOperationWatcherItem(object source, DevBoxActionToPerform actionToPerform, string id, ThreadPoolTimer timer)
    {
        ActionToPerform = actionToPerform;
        Id = id;
        Timer = timer;
        Source = source;
    }
}
