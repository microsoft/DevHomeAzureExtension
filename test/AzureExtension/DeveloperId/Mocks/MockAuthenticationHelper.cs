// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Identity.Client;
using Microsoft.UI;

namespace AzureExtension.Test.DeveloperId.Mocks;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
internal class MockAuthenticationHelper : IAuthenticationHelper
{
    private readonly List<string> loginIds = new();

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
        return loginIds;
    }

    public async void InitializePublicClientAppForWAMBrokerAsync()
    {
        return;
    }

    public async Task InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(WindowId? windowPtr)
    {
        return;
    }

    public void InitializePublicClientApplicationBuilder()
    {
    }

    public async Task<IAccount?> LoginDeveloperAccount(string[] scopes)
    {
       AuthenticationResult authResult = new AuthenticationResult(
                accessToken: string.Empty,
                isExtendedLifeTimeToken: false,
                uniqueId: string.Empty,
                expiresOn: DateTimeOffset.Now,
                extendedExpiresOn: DateTimeOffset.Now,
                tenantId: string.Empty,
                account: null,
                idToken: "id token",
                scopes: new[] { "scope1", "scope2" },
                correlationId: Guid.Empty,
                authenticationResultMetadata: null,
                tokenType: string.Empty);

       return authResult.Account;
    }

    public Task<AuthenticationResult?> ObtainTokenForLoggedInDeveloperAccount(string[] scopesArray, string loginId) => throw new NotImplementedException();

    public async Task SignOutDeveloperIdAsync(string username)
    {
        return;
    }
}
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
