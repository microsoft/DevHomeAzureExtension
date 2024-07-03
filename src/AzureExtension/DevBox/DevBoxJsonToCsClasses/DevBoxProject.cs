// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a DevBoxProject object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxProject
{
    public string Id { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DevBoxProjectProperties Properties { get; set; } = new();

    public string Type { get; set; } = string.Empty;
}
