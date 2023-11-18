// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace AzureExtension.Contracts;

public interface IDataTokenService
{
    public Task<string> GetTokenAsync();
}
