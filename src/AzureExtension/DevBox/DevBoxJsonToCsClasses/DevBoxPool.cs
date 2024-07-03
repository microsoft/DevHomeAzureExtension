// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a Pool object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxPool
{
    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string OsType { get; set; } = string.Empty;

    public DevBoxHardwareProfile HardwareProfile { get; set; } = new();

    public DevBoxStorageProfile StorageProfile { get; set; } = new();

    public string HibernateSupport { get; set; } = string.Empty;

    public DevBoxImageReference ImageReference { get; set; } = new();

    public DevBoxPoolStopOnDisconnect StopOnDisconnect { get; set; } = new();

    public string HealthStatus { get; set; } = string.Empty;
}
