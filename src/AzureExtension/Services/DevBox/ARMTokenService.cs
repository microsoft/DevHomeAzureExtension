// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

/// <summary>
/// Implementation of the Azure Resource Manager (ARM) token service.
/// It is a wrapper leveraging Developer ID's silent token aquiring
/// function, with the scope needed for ARM
/// </summary>
public class ARMTokenService : IArmTokenService
{
    private AuthenticationHelper AuthHelper => new();

    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        if (devId == null)
        {
            Log.Logger()?.ReportError($"ARMTokenService::GetTokenAsync: No dev id provided");
            return string.Empty;
        }

        var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(new string[] { Constants.ManagementPlaneScope }, devId.LoginId);
        return result?.AccessToken ?? string.Empty;
    }
}
