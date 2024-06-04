// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test.DevBox;

public class ArmTestTokenService : IArmTokenService
{
    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        await Task.Delay(0);
        return "Placeholder Test Token";
    }
}
