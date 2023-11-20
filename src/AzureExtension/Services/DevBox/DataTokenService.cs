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
        string[] scopes = { "https://devcenter.azure.com/access_as_user" };
        string id = devId?.LoginId ?? "modanish@microsoft.com";
        var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(scopes, id);
        return result?.AccessToken ?? string.Empty;
    }
}
