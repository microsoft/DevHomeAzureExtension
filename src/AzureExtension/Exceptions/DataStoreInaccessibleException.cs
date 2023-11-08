// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

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
