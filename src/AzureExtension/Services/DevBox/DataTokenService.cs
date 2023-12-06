// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

/// <summary>
/// Implementation of Dev Box Data Plane token service.
/// It is a wrapper leveraging Developer ID's silent token aquiring
/// function with the scope needed for the plane.
/// </summary>
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

        try
        {
            var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(new string[] { Constants.DataPlaneScope }, devId.LoginId);
            return result?.AccessToken ?? string.Empty;
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"DataTokenService::GetTokenAsync: {e.ToString}");
            return string.Empty;
        }
    }
}
