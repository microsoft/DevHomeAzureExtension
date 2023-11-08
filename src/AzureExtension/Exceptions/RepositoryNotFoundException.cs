// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension;

public class RepositoryNotFoundException : ApplicationException
{
    public RepositoryNotFoundException()
    {
    }

    public RepositoryNotFoundException(string message)
        : base(message)
    {
    }
}
