// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Exceptions;

public class DevBoxNameInvalidException : Exception
{
    public DevBoxNameInvalidException(string message)
        : base(message)
    {
    }
}
