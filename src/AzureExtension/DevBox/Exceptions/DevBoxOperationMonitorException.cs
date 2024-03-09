// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Exceptions;

public class DevBoxOperationMonitorException : Exception
{
    public DevBoxOperationMonitorException(string message)
        : base(message)
    {
    }
}
