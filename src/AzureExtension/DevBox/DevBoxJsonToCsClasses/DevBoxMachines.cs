// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the response from the Dev Center API for getting a list of DevBoxes
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxMachines
{
    public DevBoxMachineState[]? Value { get; set; }
}
