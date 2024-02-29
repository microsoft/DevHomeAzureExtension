// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AzureExtension.DevBox.Models;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the DevBoxOperation object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxOperation : DevCenterOperationBase
{
    public string Uri { get; set; } = string.Empty;

    public string OperationId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string CreatedByObjectId { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DevBoxOperationResult? Result { get; set; }
}
