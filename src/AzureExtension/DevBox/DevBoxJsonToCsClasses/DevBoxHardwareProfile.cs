// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the HardwareProfile object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxHardwareProfile
{
    [JsonPropertyName("vCPUs")]
    public int VCPUs { get; set; }

    public string SkuName { get; set; } = string.Empty;

    public int MemoryGB { get; set; }
}
