// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

public class DevBoxProjectProperties
{
    public string ProvisioningState { get; set; } = string.Empty;

    public string DevCenterUri { get; set; } = string.Empty;

    public string DevCenterId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
