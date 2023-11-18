// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class DataTokenService : IDataTokenService
{
    public Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        throw new NotImplementedException();
    }
}
