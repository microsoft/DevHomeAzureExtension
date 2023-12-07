// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.UI;

namespace DevHomeAzureExtension.DeveloperId;

public class AuthenticationHelper : IAuthenticationHelper
{
    public AuthenticationSettings MicrosoftEntraIdSettings
    {
        get; set;
    }

    private PublicClientApplicationBuilder? PublicClientApplicationBuilder
    {
        get; set;
    }

    public IPublicClientApplication? PublicClientApplication
    {
        get; private set;
    }

    public AuthenticationResult? AuthenticationResult
    {
        get; private set;
    }

    public static Guid MSATenetId { get; } = new("9188040d-6c67-4c5b-b112-36a304b66dad");

    public static Guid TransferTenetId { get; } = new("f8cdef31-a31e-4b4a-93e4-5f571e91255a");

    public AuthenticationHelper()
    {
        MicrosoftEntraIdSettings = new AuthenticationSettings();

        InitializePublicClientApplicationBuilder();

        InitializePublicClientAppForWAMBrokerAsync();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Globalization not required as the strings are not being displayed to user and are used in initialization")]
    public void InitializePublicClientApplicationBuilder()
    {
        MicrosoftEntraIdSettings.InitializeSettings();
        Log.Logger()?.ReportInfo($"Initialized MicrosoftEntraIdSettings");

        PublicClientApplicationBuilder = PublicClientApplicationBuilder.Create(MicrosoftEntraIdSettings.ClientId)
           .WithAuthority(string.Format(MicrosoftEntraIdSettings.Authority, MicrosoftEntraIdSettings.TenantId))
           .WithRedirectUri(string.Format(MicrosoftEntraIdSettings.RedirectURI, MicrosoftEntraIdSettings.ClientId))
           .WithLogging(new MSALLogger(EventLogLevel.Warning), enablePiiLogging: false)
           .WithClientCapabilities(new string[] { "cp1" });
        Log.Logger()?.ReportInfo($"Created PublicClientApplicationBuilder");
    }

    public async void InitializePublicClientAppForWAMBrokerAsync()
    {
        if (PublicClientApplicationBuilder != null)
        {
            PublicClientApplicationBuilder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                MsaPassthrough = true,
                Title = "DevHomeAzureExtension",
            });

            PublicClientApplication = PublicClientApplicationBuilder.Build();
            Log.Logger()?.ReportInfo($"PublicClientApplication is instantiated");
        }
        else
        {
            Log.Logger()?.ReportError($"PublicClientApplicationBuilder is not initialized");
            throw new ArgumentNullException(null);
        }

        await TokenCacheRegistration();
    }

    public async Task InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(WindowId? windowPtr)
    {
        if (windowPtr != null && PublicClientApplicationBuilder != null)
        {
            var windowHandle = Win32Interop.GetWindowFromWindowId((WindowId)windowPtr);
            var builder = PublicClientApplicationBuilder
                .WithParentActivityOrWindow(() => { return windowHandle; });

            builder = builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
            {
                MsaPassthrough = true,
                Title = "Dev Home Azure Extension",
            });

            PublicClientApplication = PublicClientApplicationBuilder.Build();
            Log.Logger()?.ReportInfo($"PublicClientApplication is instantiated for interactive UI capability");
        }
        else
        {
            Log.Logger()?.ReportError($"Incorrect parameters to instantiate PublicClientApplication with interactive UI capability");
            throw new ArgumentNullException(null);
        }

        await TokenCacheRegistration();
    }

    private async Task<IEnumerable<IAccount>> TokenCacheRegistration()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(MicrosoftEntraIdSettings.CacheFileName, MicrosoftEntraIdSettings.CacheDir).Build();
        var msalcachehelper = await MsalCacheHelper.CreateAsync(storageProperties);
        if (PublicClientApplication != null)
        {
            msalcachehelper.RegisterCache(PublicClientApplication.UserTokenCache);
            Log.Logger()?.ReportInfo($"Token cache is successfully registered with PublicClientApplication");

            // In the case the cache file is being reused there will be preexisting logged in accounts
            return await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);
        }
        else
        {
            Log.Logger()?.ReportError($"Error encountered while registering token cache");
        }

        return Enumerable.Empty<IAccount>();
    }

    public async Task<IAccount?> GetDeveloperAccountFromCache(string loginid)
    {
        IEnumerable<IAccount> accounts = new List<IAccount>();

        if (PublicClientApplication != null)
        {
            accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);
        }

        foreach (var account in accounts)
        {
            if (account.Username == loginid)
            {
                return account;
            }
        }

        return null;
    }

    public async Task<IEnumerable<string>> GetAllStoredLoginIdsAsync()
    {
        var loginIds = new List<string>();
        IEnumerable<IAccount> accounts;

        if (PublicClientApplication != null)
        {
            accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);

            foreach (var account in accounts)
            {
                loginIds.Add(account.Username);
            }
        }

        return loginIds;
    }

    public async Task<IAccount?> AcquireWindowsAccountTokenSilently(string[] scopes)
    {
        // The below code will only acquire the default Window account token on first launch
        // i.e. token cache is empty and user has not explicitly signed out of the account
        // in the application.
        // If a user has explicitly signed out of the connected Windows account, WAM will
        // remember and not acquire a token on behalf of a user.
        Log.Logger()?.ReportInfo($"Enable SSO for Azure Extension by connecting the Windows's default account");
        AuthenticationResult = null;
        try
        {
            if (PublicClientApplication != null)
            {
                var accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);
                if (!accounts.Any())
                {
                    var silentTokenAcquisitionBuilder = PublicClientApplication.AcquireTokenSilent(scopes, Microsoft.Identity.Client.PublicClientApplication.OperatingSystemAccount);
                    if (Guid.TryParse(Microsoft.Identity.Client.PublicClientApplication.OperatingSystemAccount.HomeAccountId.TenantId, out var homeTenantId) && homeTenantId == MSATenetId)
                    {
                        silentTokenAcquisitionBuilder = silentTokenAcquisitionBuilder.WithTenantId(TransferTenetId.ToString("D"));
                    }

                    AuthenticationResult = await silentTokenAcquisitionBuilder.ExecuteAsync();
                    Log.Logger()?.ReportInfo($"Azure SSO enabled");
                    return AuthenticationResult.Account;
                }
            }
        }
        catch (Exception ex)
        {
            // This is best effort
            Log.Logger()?.ReportInfo($"Azure SSO failed with exception:{ex}");
        }

        return null;
    }

    public async Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(string[] scopes)
    {
        Log.Logger()?.ReportInfo($"AcquireAllDeveloperAccountTokens");
        var remediationAccountIdentifiers = new List<string>();
        if (PublicClientApplication != null)
        {
            var accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);

            foreach (var account in accounts)
            {
                var silentTokenAcquisitionBuilder = PublicClientApplication.AcquireTokenSilent(scopes, account);
                if (Guid.TryParse(account.HomeAccountId.TenantId, out var homeTenantId) && homeTenantId == MSATenetId)
                {
                    silentTokenAcquisitionBuilder = silentTokenAcquisitionBuilder.WithTenantId(TransferTenetId.ToString("D"));
                }

                try
                {
                    await silentTokenAcquisitionBuilder.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    Log.Logger()?.ReportInfo($"AcquireAllDeveloperAccountTokens failed {ex}");
                    remediationAccountIdentifiers.Add(account.Username);
                }
            }
        }

        return remediationAccountIdentifiers;
    }

    private async Task<AuthenticationResult?> InitiateAuthenticationFlowAsync(string[] scopes)
    {
        AuthenticationResult = null;
        if (PublicClientApplication.IsUserInteractive() && PublicClientApplication != null)
        {
            try
            {
                AuthenticationResult = await PublicClientApplication.AcquireTokenInteractive(scopes).ExecuteAsync();
            }
            catch (MsalClientException msalClientEx)
            {
                if (msalClientEx.ErrorCode == MsalError.AuthenticationCanceledError)
                {
                    Log.Logger()?.ReportInfo($"MSALClient: User canceled authentication:{msalClientEx}");
                }
                else
                {
                    Log.Logger()?.ReportError($"MSALClient: Error Acquiring Token:{msalClientEx}");
                }
            }
            catch (MsalException msalEx)
            {
                Log.Logger()?.ReportError($"MSAL: Error Acquiring Token:{msalEx}");
            }
            catch (Exception auhenticationException)
            {
                Log.Logger()?.ReportError($"Authentication: Error Acquiring Token:{auhenticationException}");
            }

            Log.Logger()?.ReportInfo($"MSAL: Signed in user by acquiring token interactively.");
        }

        return AuthenticationResult;
    }

    public async Task<IAccount?> LoginDeveloperAccount(string[] scopes)
    {
        await InitiateAuthenticationFlowAsync(scopes);
        if (AuthenticationResult != null)
        {
            return AuthenticationResult.Account;
        }

        return null;
    }

    public async Task<AuthenticationResult?> ObtainTokenForLoggedInDeveloperAccount(string[] scopes, string loginId)
    {
        Log.Logger()?.ReportInfo($"ObtainTokenForLoggedInDeveloperAccount");
        AuthenticationResult = null;

        var existingAccount = await GetDeveloperAccountFromCache(loginId);

        if (PublicClientApplication != null && existingAccount != null)
        {
            var silentTokenAcquisitionBuilder = PublicClientApplication.AcquireTokenSilent(scopes, existingAccount);
            if (Guid.TryParse(existingAccount.HomeAccountId.TenantId, out var homeTenantId) && homeTenantId == MSATenetId)
            {
                silentTokenAcquisitionBuilder = silentTokenAcquisitionBuilder.WithTenantId(TransferTenetId.ToString("D"));
            }

            try
            {
                AuthenticationResult = await silentTokenAcquisitionBuilder.ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed and requires user interaction {ex}");
                throw;
            }
            catch (MsalServiceException ex)
            {
                Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with msal service error: {ex}");
                throw;
            }
            catch (MsalClientException ex)
            {
                Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with msal client error: {ex}");
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with error: {ex}");
                throw;
            }
        }

        if (AuthenticationResult != null)
        {
            return AuthenticationResult;
        }

        return null;
    }

    public async Task SignOutDeveloperIdAsync(string username)
    {
        IEnumerable<IAccount> accounts;
        if (PublicClientApplication != null)
        {
            accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);

            foreach (var account in accounts)
            {
                if (account.Username == username)
                {
                    await PublicClientApplication.RemoveAsync(account).ConfigureAwait(false);
                    Log.Logger()?.ReportInfo($"MSAL: Signed out user.");
                }
                else
                {
                    Log.Logger()?.ReportWarn($"MSAL: User is already absent from cache .");
                }
            }
        }
        else
        {
            Log.Logger()?.ReportError($"Cannot login developer id as PublicClientApplication is null");
            throw new ArgumentNullException(null);
        }
    }
}
