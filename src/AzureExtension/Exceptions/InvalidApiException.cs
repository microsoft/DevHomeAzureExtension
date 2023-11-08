// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension;

public class InvalidApiException : Exception
{
    public InvalidApiException()
    {
    }

    public InvalidApiException(string message)
        : base(message)
    {
    }
}
