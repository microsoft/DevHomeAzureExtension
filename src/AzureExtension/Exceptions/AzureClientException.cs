// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
