// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

public class DevBoxOperationList
{
    public List<DevBoxOperation> Value { get; set; } = new List<DevBoxOperation>();
}
