// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using DevBoxConstants = DevHomeAzureExtension.DevBox.Constants;

namespace DevHomeAzureExtension.Services.DevBox;

/// <summary>
/// Implementation of Dev Box Data Plane token service.
/// It is a wrapper leveraging Developer ID's silent token acquiring
/// function with the scope needed for the plane.
/// </summary>
public class DataTokenService : IDataTokenService
{
    private AuthenticationHelper AuthHelper => new();

    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        if (devId == null)
        {
            Log.Error($"DataTokenService::GetTokenAsync: No dev id provided");
            return string.Empty;
        }

        try
        {
            var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(new string[] { DevBoxConstants.DataPlaneScope }, devId.LoginId);
            return result?.AccessToken ?? string.Empty;
        }
        catch (Exception e)
        {
            Log.Error($"DataTokenService::GetTokenAsync: {e.ToString}");
            return string.Empty;
        }
    }
}
