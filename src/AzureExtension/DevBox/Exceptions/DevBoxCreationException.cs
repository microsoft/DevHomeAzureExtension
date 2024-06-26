// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Exceptions;

public class DevBoxCreationException : Exception
{
    public DevBoxCreationException(string message)
        : base(message)
    {
    }
}
