// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AzureExtension.DevBox.Models;

public enum DevCenterOperationStatus
{
    NotStarted,
    Running,
    Succeeded,
    Canceled,
    Failed,
}

public class DevCenterOperationBase
{
    public DevCenterOperationStatus? Status { get; set; }

    public DateTime StartTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EndTime { get; set; }
}
