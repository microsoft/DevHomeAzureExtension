// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary> See API documentation <see cref="Constants.APIVersion"/> </summary>
public class DevBoxHardwareProfile
{
    [JsonPropertyName("vCPUs")]
    public int VCPUs { get; set; }

    public string SkuName { get; set; } = string.Empty;

    public int MemoryGB { get; set; }
}
