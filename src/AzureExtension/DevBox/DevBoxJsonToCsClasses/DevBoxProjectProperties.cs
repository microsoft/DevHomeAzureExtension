// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a ProjectProperties object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxProjectProperties
{
    public string ProvisioningState { get; set; } = string.Empty;

    public string DevCenterUri { get; set; } = string.Empty;

    public string DevCenterId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
