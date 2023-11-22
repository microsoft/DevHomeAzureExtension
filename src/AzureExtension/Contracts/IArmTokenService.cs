// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IArmTokenService
{
    public Task<string> GetTokenAsync(IDeveloperId? devId);
}
