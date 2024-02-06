// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension;

public class DataStoreInaccessibleException : ApplicationException
{
    public DataStoreInaccessibleException()
    {
    }

    public DataStoreInaccessibleException(string message)
        : base(message)
    {
    }
}
