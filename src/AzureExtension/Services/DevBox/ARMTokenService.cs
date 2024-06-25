// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using DevBoxConstants = DevHomeAzureExtension.DevBox.Constants;

namespace DevHomeAzureExtension.Services.DevBox;

/// <summary>
/// Implementation of the Azure Resource Manager (ARM) token service.
/// It is a wrapper leveraging Developer ID's silent token acquiring
/// function, with the scope needed for ARM
/// </summary>
public class ARMTokenService : IArmTokenService
{
    private AuthenticationHelper AuthHelper => new();

    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        if (devId == null)
        {
            Log.Error($"ARMTokenService::GetTokenAsync: No dev id provided");
            return string.Empty;
        }

        var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(new string[] { DevBoxConstants.ManagementPlaneScope }, devId.LoginId);
        return result?.AccessToken ?? string.Empty;
    }
}
