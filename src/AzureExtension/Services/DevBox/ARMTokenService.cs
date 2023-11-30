// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

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

        string[] scopes = { "https://management.azure.com/user_impersonation" };
        try
        {
            var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(scopes, devId.LoginId);
            return result?.AccessToken ?? string.Empty;
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"ARMTokenService::GetTokenAsync: {e.Message}");
            return string.Empty;
        }
    }
}
