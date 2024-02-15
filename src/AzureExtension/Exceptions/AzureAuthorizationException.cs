// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
