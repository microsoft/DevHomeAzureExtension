// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a DevBoxOperationResult object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxOperationResult
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string RepairOutcome { get; set; } = string.Empty;
}
