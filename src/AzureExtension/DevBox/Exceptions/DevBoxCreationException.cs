// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureExtension.DevBox.Exceptions;

public class DevBoxCreationException : Exception
{
    public DevBoxCreationException(string message)
        : base(message)
    {
    }
}
