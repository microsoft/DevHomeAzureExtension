// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Exceptions;

public class DevBoxNameInvalidException : Exception
{
    public DevBoxNameInvalidException(string message)
        : base(message)
    {
    }
}
