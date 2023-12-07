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

internal class MockAuthenticationHelper : IAuthenticationHelper
{
    public AuthenticationSettings MicrosoftEntraIdSettings
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(object scopesArray) => throw new NotImplementedException();

    public Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(string[] scopes) => throw new NotImplementedException();

    public Task<IAccount?> AcquireWindowsAccountTokenSilently(string[] scopes) => throw new NotImplementedException();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<IEnumerable<string>> GetAllStoredLoginIdsAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var loginIds = new List<string>
        {
            "testAccount",
            "testAccount2",
            "testAccount3",
        };

        return loginIds;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async void InitializePublicClientAppForWAMBrokerAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        return;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(WindowId? windowPtr)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        return;
    }

    public void InitializePublicClientApplicationBuilder()
    {
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task<IAccount?> LoginDeveloperAccount(string[] scopes)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task SignOutDeveloperIdAsync(string username)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        return;
    }
}
