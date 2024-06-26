// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Exceptions;

public class DevBoxOperationMonitorException : Exception
{
    public DevBoxOperationMonitorException(string message)
        : base(message)
    {
    }
}
