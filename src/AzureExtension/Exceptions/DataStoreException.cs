// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

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
