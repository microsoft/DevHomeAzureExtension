// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension;

public class DataStoreException : Exception
{
    public DataStoreException()
    {
    }

    public DataStoreException(string message)
        : base(message)
    {
    }
}
