// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary> See API documentation <see cref="Constants.APIVersion"/> </summary>
public class DevBoxStorageProfile
{
    public DevBoxOsDisk OsDisk { get; set; } = new();
}
