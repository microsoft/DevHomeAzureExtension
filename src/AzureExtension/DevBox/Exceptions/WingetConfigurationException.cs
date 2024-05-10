// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Exceptions;

public class WingetConfigurationException : Exception
{
    public WingetConfigurationException(string message)
        : base(message)
    {
    }
}
