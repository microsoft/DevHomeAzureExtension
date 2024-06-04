// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// This class represents the state of a DevBox machine. It is the class representation of
/// the JSON response from the DevBox API. It is a snapshot of the state of the DevBox machine
/// at the time of the response from the Dev Center.
/// Should be kept in sync with the DevBox Json api definition. <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxMachineState
{
    public string Uri { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string PoolName { get; set; } = string.Empty;

    public string HibernateSupport { get; set; } = string.Empty;

    public string ProvisioningState { get; set; } = string.Empty;

    public string ActionState { get; set; } = string.Empty;

    public string PowerState { get; set; } = string.Empty;

    public string UniqueId { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string OsType { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public DevBoxHardwareProfile HardwareProfile { get; set; } = new();

    public DevBoxStorageProfile StorageProfile { get; set; } = new();

    public DevBoxImageReference ImageReference { get; set; } = new();

    public string LocalAdministrator { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}
