// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Contracts;

public interface IDataTokenService
{
    public Task<string> GetTokenAsync(IDeveloperId? devId);
}
