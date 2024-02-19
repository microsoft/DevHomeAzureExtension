// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary> See API documentation <see cref="Constants.APIVersion"/> </summary>
public class DevBoxImageReference
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string OsBuildNumber { get; set; } = string.Empty;

    public DateTime PublishedDate { get; set; }
}
