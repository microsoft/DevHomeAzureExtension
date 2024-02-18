// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.DeveloperId;
using Microsoft.Identity.Client;
using Microsoft.UI;

namespace DevHomeAzureExtension.Test.DeveloperId.Mocks;

internal sealed class MockAuthenticationHelper : IAuthenticationHelper
{
    private readonly List<string> loginIds = new();

    private static readonly string[] _scopes = new[] { "scope1", "scope2" };

    public AuthenticationSettings MicrosoftEntraIdSettings
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(object scopesArray) => throw new NotImplementedException();

    public Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(string[] scopes) => throw new NotImplementedException();

    public Task<IAccount?> AcquireWindowsAccountTokenSilently(string[] scopes) => throw new NotImplementedException();

    public void PopulateCache()
    {
        loginIds.Add("testAccount");
    }

    public async Task<IEnumerable<string>> GetAllStoredLoginIdsAsync()
    {
        await Task.Run(() => { });
        return loginIds;
    }

    public async void InitializePublicClientAppForWAMBrokerAsync()
    {
        await Task.Run(() => { });
        return;
    }

    public async Task InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(WindowId? windowPtr)
    {
        await Task.Run(() => { });
        return;
    }

    public void InitializePublicClientApplicationBuilder()
    {
    }

    public async Task<IAccount?> LoginDeveloperAccount(string[] scopes)
    {
        await Task.Run(() => { });
        AuthenticationResult authResult = new AuthenticationResult(
                accessToken: string.Empty,
                isExtendedLifeTimeToken: false,
                uniqueId: string.Empty,
                expiresOn: DateTimeOffset.Now,
                extendedExpiresOn: DateTimeOffset.Now,
                tenantId: string.Empty,
                account: null,
                idToken: "id token",
                scopes: _scopes,
                correlationId: Guid.Empty,
                authenticationResultMetadata: null,
                tokenType: string.Empty);

        return authResult.Account;
    }

    public Task<AuthenticationResult?> ObtainTokenForLoggedInDeveloperAccount(string[] scopesArray, string loginId) => throw new NotImplementedException();

    public async Task SignOutDeveloperIdAsync(string username)
    {
        await Task.Run(() => { });
        return;
    }
}
