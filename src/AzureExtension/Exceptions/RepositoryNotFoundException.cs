// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
