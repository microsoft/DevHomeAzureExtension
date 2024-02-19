// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

public class DevBoxMachines
{
    public DevBoxMachineState[]? Value { get; set; }
}
