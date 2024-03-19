// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Exceptions;

public class DevBoxCreationException : Exception
{
    public DevBoxCreationException(string message)
        : base(message)
    {
    }
}
