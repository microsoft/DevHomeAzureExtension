// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the ImageReference object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxImageReference
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string OsBuildNumber { get; set; } = string.Empty;

    public DateTime PublishedDate { get; set; }
}
