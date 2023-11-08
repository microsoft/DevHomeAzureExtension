// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension;

public class AzureAuthorizationException : Exception
{
    public AzureAuthorizationException()
    {
    }

    public AzureAuthorizationException(string message)
        : base(message)
    {
    }
}
