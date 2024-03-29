﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Test.DevBox;

public class DataTestTokenService : IDataTokenService
{
    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        await Task.Delay(0);
        return "Placeholder Test Token";
    }
}
