// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension;

public class AzureClientException : Exception
{
    public AzureClientException()
    {
    }

    public AzureClientException(string message)
        : base(message)
    {
    }
}
