// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a list of Pool object's within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxPoolRoot
{
    public List<DevBoxPool>? Value { get; set; }
}
