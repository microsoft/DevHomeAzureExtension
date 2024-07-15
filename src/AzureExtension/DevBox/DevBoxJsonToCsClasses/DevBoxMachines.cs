// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the response from the Dev Center API for getting a list of DevBoxes
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxMachines
{
    public DevBoxMachineState[]? Value { get; set; }
}
