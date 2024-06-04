// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DevHomeAzureExtension.DevBox.Models;

/// <summary>
/// The status of a DevCenter operation.
/// See API documentation <see cref="Constants.APIVersion"/> for more information.
/// </summary>
public enum DevCenterOperationStatus
{
    NotStarted,
    Running,
    Succeeded,
    Canceled,
    Failed,
}

/// <summary>
/// For all DevCenter operations, this class is used to store the common properties.
/// It is used when querying the status of an operation.
/// </summary>
public class DevCenterOperationBase
{
    public DevCenterOperationStatus? Status { get; set; }

    public DateTime StartTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EndTime { get; set; }

    public override string ToString()
    {
        return $"Status: {Status}, StartTime: {StartTime}, EndTime: {EndTime}";
    }
}
