// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// https://azure.github.io/typespec-azure/docs/howtos/Azure%20Core/long-running-operations for move information
/// on long running operations. This class is used to represent a long running operation.
/// </summary>
public class DevBoxLongRunningOperation
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public static DevBoxLongRunningOperation CreateFailureOperation()
    {
        var operation = new DevBoxLongRunningOperation();
        operation.Status = Constants.DevBoxOperationFailedState;
        return operation;
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, Constants.JsonOptions);
    }
}
