// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class DataTokenService : IDataTokenService
{
    private AuthenticationHelper AuthHelper => new();

    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        if (devId == null)
        {
            Log.Logger()?.ReportError($"DataTokenService::GetTokenAsync: No dev id provided");
            return string.Empty;
        }

        string[] scopes = { "https://devcenter.azure.com/access_as_user" };
        try
        {
            var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(scopes, devId.LoginId);
            return result?.AccessToken ?? string.Empty;
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"DataTokenService::GetTokenAsync: {e.Message}");
            return string.Empty;
        }
    }
}
