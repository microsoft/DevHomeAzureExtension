// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Exceptions;

public class DevBoxProjectNameInvalidException : Exception
{
    public DevBoxProjectNameInvalidException(string message)
        : base(message)
    {
    }
}
