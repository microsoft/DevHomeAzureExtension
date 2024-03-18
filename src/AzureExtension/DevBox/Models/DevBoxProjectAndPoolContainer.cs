// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.DevBox.DevBoxJsonToCsClasses;

namespace AzureExtension.DevBox.Models;

/// <summary>
/// Represents an object that contains both a DevBoxProject and a DevBoxPoolRoot.
/// There is no API that combines these two objects, so this is a custom object.
/// </summary>
public class DevBoxProjectAndPoolContainer
{
    public DevBoxProject? Project { get; set; }

    public DevBoxPoolRoot? Pools { get; set; }
}
