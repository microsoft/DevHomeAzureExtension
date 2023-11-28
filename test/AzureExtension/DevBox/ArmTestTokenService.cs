// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Test.DevBox;

public class ArmTestTokenService : IArmTokenService
{
    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        await Task.Delay(0);
        return "Placeholder Test Token";
    }
}
