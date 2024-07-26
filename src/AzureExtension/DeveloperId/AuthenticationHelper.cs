// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.UI;
using Serilog;

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

    private static readonly string[] _capabilities = ["cp1"];

    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(AuthenticationHelper)));

    private static readonly ILogger _log = _logger.Value;

    public AuthenticationHelper()
    {
        MicrosoftEntraIdSettings = new AuthenticationSettings();

        InitializePublicClientApplicationBuilder();

        InitializePublicClientAppForWAMBrokerAsync();
    }

    public void InitializePublicClientApplicationBuilder()
    {
        MicrosoftEntraIdSettings.InitializeSettings();
        _log.Debug($"Initialized MicrosoftEntraIdSettings");

        PublicClientApplicationBuilder = PublicClientApplicationBuilder.Create(MicrosoftEntraIdSettings.ClientId)
           .WithAuthority(string.Format(CultureInfo.InvariantCulture, MicrosoftEntraIdSettings.Authority, MicrosoftEntraIdSettings.TenantId))
           .WithRedirectUri(string.Format(CultureInfo.InvariantCulture, MicrosoftEntraIdSettings.RedirectURI, MicrosoftEntraIdSettings.ClientId))
           .WithLogging(new MSALLogger(EventLogLevel.Warning), enablePiiLogging: false)
           .WithClientCapabilities(_capabilities);
        _log.Debug($"Created PublicClientApplicationBuilder");
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
            _log.Debug($"PublicClientApplication is instantiated");
        }
        else
        {
            _log.Error($"PublicClientApplicationBuilder is not initialized");
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
            _log.Debug($"PublicClientApplication is instantiated for interactive UI capability");
        }
        else
        {
            _log.Error($"Incorrect parameters to instantiate PublicClientApplication with interactive UI capability");
            throw new ArgumentNullException(null);
        }

        await TokenCacheRegistration();
    }

    private async Task<IEnumerable<IAccount>> TokenCacheRegistration()
    {
        var storageProperties = new StorageCreationPropertiesBuilder(MicrosoftEntraIdSettings.CacheFileName, MicrosoftEntraIdSettings.CacheDir).Build();
        var msalCacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        if (PublicClientApplication != null)
        {
            msalCacheHelper.RegisterCache(PublicClientApplication.UserTokenCache);
            _log.Debug($"Token cache is successfully registered with PublicClientApplication");

            // In the case the cache file is being reused there will be preexisting logged in accounts
            return await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);
        }
        else
        {
            _log.Error($"Error encountered while registering token cache");
        }

        return Enumerable.Empty<IAccount>();
    }

    public async Task<IAccount?> GetDeveloperAccountFromCache(string loginId)
    {
        IEnumerable<IAccount> accounts = new List<IAccount>();

        if (PublicClientApplication != null)
        {
            accounts = await PublicClientApplication.GetAccountsAsync().ConfigureAwait(false);
        }

        foreach (var account in accounts)
        {
            if (account.Username == loginId)
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
        _log.Debug($"Enable SSO for Azure Extension by connecting the Windows's default account");
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
                    _log.Information($"Azure SSO enabled");
                    return AuthenticationResult.Account;
                }
            }
        }
        catch (Exception ex)
        {
            // This is best effort
            _log.Information($"Azure SSO failed with exception:{ex}");
        }

        return null;
    }

    public async Task<IEnumerable<string>> AcquireAllDeveloperAccountTokens(string[] scopes)
    {
        _log.Debug($"AcquireAllDeveloperAccountTokens");
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
                    _log.Information($"AcquireAllDeveloperAccountTokens failed {ex}");
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
                    _log.Information($"MSALClient: User canceled authentication:{msalClientEx}");
                }
                else
                {
                    _log.Error($"MSALClient: Error Acquiring Token:{msalClientEx}");
                }
            }
            catch (MsalException msalEx)
            {
                _log.Error($"MSAL: Error Acquiring Token:{msalEx}");
            }
            catch (Exception authenticationException)
            {
                _log.Error($"Authentication: Error Acquiring Token:{authenticationException}");
            }

            _log.Information($"MSAL: Signed in user by acquiring token interactively.");
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
        _log.Debug($"ObtainTokenForLoggedInDeveloperAccount");
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
                _log.Error($"AcquireDeveloperAccountToken failed and requires user interaction {ex}");
                throw;
            }
            catch (MsalServiceException ex)
            {
                _log.Error($"AcquireDeveloperAccountToken failed with MSAL service error: {ex}");
                throw;
            }
            catch (MsalClientException ex)
            {
                _log.Error($"AcquireDeveloperAccountToken failed with MSAL client error: {ex}");
                throw;
            }
            catch (Exception ex)
            {
                _log.Error($"AcquireDeveloperAccountToken failed with error: {ex}");
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
                    _log.Information($"MSAL: Signed out user.");
                }
                else
                {
                    _log.Warning($"MSAL: User is already absent from cache .");
                }
            }
        }
        else
        {
            _log.Error($"Cannot login developer id as PublicClientApplication is null");
            throw new ArgumentNullException(null);
        }
    }
}
