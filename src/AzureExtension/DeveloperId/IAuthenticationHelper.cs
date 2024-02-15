// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.UI;

namespace DevHomeAzureExtension.DeveloperId;

public interface IAuthenticationHelper
{
    public AuthenticationSettings MicrosoftEntraIdSettings
    {
        get; set;
    }

    public void InitializePublicClientApplicationBuilder();

    public void InitializePublicClientAppForWAMBrokerAsync();

    public Task<IEnumerable<string>> GetAllStoredLoginIdsAsync();

    public Task InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(WindowId? windowPtr);

    public Task<IAccount?> LoginDeveloperAccount(string[] scopes);

    public Task SignOutDeveloperIdAsync(string username);

    public Task<IAccount?> AcquireWindowsAccountTokenSilently(string[] scopes);

    public Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(string[] scopes);

    public Task<AuthenticationResult?> ObtainTokenForLoggedInDeveloperAccount(string[] scopes, string loginId);
}
