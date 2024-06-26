// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a list of DevBoxOperation object's within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxOperationList
{
    public List<DevBoxOperation> Value { get; set; } = new List<DevBoxOperation>();
}
