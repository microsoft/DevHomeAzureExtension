// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.UI;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.DeveloperId;

public class DeveloperIdProvider : IDeveloperIdProvider
{
    // Locks to control access to Singleton class members.
    private static readonly object _developerIdsLock = new();

    private static readonly object _authenticationProviderLock = new();

    // DeveloperId list containing all Logged in Ids.
    private List<DeveloperId> DeveloperIds
    {
        get; set;
    }

    private IAuthenticationHelper DeveloperIdAuthenticationHelper
    {
        get; set;
    }

    // DeveloperIdProvider uses singleton pattern.
    private static DeveloperIdProvider? _singletonDeveloperIdProvider;

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DeveloperIdProvider));

    public event TypedEventHandler<IDeveloperIdProvider, IDeveloperId>? Changed;

    private readonly AuthenticationExperienceKind _authenticationExperienceForAzureExtension = AuthenticationExperienceKind.CustomProvider;

    public string DisplayName => "Azure";

    // Private constructor for Singleton class.
    private DeveloperIdProvider(IAuthenticationHelper authenticationHelper)
    {
        _log.Debug($"Creating DeveloperIdProvider singleton instance");

        lock (_developerIdsLock)
        {
            DeveloperIds ??= new List<DeveloperId>();

            DeveloperIdAuthenticationHelper = authenticationHelper;

            // Retrieve and populate Logged in DeveloperIds from previous launch.
            RestoreDeveloperIds(DeveloperIdAuthenticationHelper.GetAllStoredLoginIdsAsync());
        }
    }

    public void EnableSSOForAzureExtensionAsync()
    {
        var account = DeveloperIdAuthenticationHelper.AcquireWindowsAccountTokenSilently(DeveloperIdAuthenticationHelper.MicrosoftEntraIdSettings.ScopesArray);
        if (account.Result != null)
        {
            _ = CreateOrUpdateDeveloperId(account.Result);
            _log.Debug($"SSO For Azure Extension");
        }
    }

    // Retrieve access tokens for all accounts silently to determine application state
    // DevHome can use this information to inform and prompt user of next steps
    public DeveloperIdsResult DetermineAccountRemediationForAzureExtensionAsync()
    {
        var developerIds = new List<DeveloperId>();
        var resultForAccountsToFix = DeveloperIdAuthenticationHelper.AcquireAllDeveloperAccountTokens(DeveloperIdAuthenticationHelper.MicrosoftEntraIdSettings.ScopesArray);
        var accountsToFix = resultForAccountsToFix.Result;
        if (accountsToFix.Any())
        {
            foreach (var account in accountsToFix)
            {
                var devId = GetDeveloperIdFromAccountIdentifier(account);
                if (devId != null)
                {
                    developerIds.Add(devId);
                }
                else
                {
                    _log.Warning($"DeveloperId not found to remediate");
                }
            }

            return new DeveloperIdsResult(developerIds);
        }

        return new DeveloperIdsResult(null, "No account remediation required");
    }

    public static DeveloperIdProvider GetInstance(IAuthenticationHelper? authenticationHelper = null)
    {
        authenticationHelper ??= new AuthenticationHelper();

        lock (_authenticationProviderLock)
        {
            _singletonDeveloperIdProvider ??= new DeveloperIdProvider(authenticationHelper);
        }

        return _singletonDeveloperIdProvider;
    }

    public DeveloperIdsResult GetLoggedInDeveloperIds()
    {
        List<IDeveloperId> iDeveloperIds = new();
        lock (_developerIdsLock)
        {
            iDeveloperIds.AddRange(DeveloperIds);
        }

        var developerIdsResult = new DeveloperIdsResult(iDeveloperIds);

        return developerIdsResult;
    }

    public IAsyncOperation<DeveloperIdResult> ShowLogonSession(WindowId windowHandle)
    {
        return Task.Run(async () =>
        {
            await DeveloperIdAuthenticationHelper.InitializePublicClientAppForWAMBrokerAsyncWithParentWindow(windowHandle);
            var account = DeveloperIdAuthenticationHelper.LoginDeveloperAccount(DeveloperIdAuthenticationHelper.MicrosoftEntraIdSettings.ScopesArray);

            if (account.Result == null)
            {
                _log.Error($"Invalid AuthRequest");
                var exception = new InvalidOperationException();
                return new DeveloperIdResult(exception, "An issue has occurred with the authentication request");
            }

            var devId = CreateOrUpdateDeveloperId(account.Result);
            _log.Information($"New DeveloperId logged in");
            return new DeveloperIdResult(devId);
        }).AsAsyncOperation();
    }

    public ProviderOperationResult LogoutDeveloperId(IDeveloperId developerId)
    {
        DeveloperId? developerIdToLogout;
        lock (_developerIdsLock)
        {
            developerIdToLogout = DeveloperIds?.Find(e => e.LoginId == developerId.LoginId);
            if (developerIdToLogout == null)
            {
                _log.Error($"Unable to find DeveloperId to logout");
                return new ProviderOperationResult(ProviderOperationStatus.Failure, new ArgumentNullException(nameof(developerId)), "The developer account to log out does not exist", "Unable to find DeveloperId to logout");
            }

            var result = DeveloperIdAuthenticationHelper.SignOutDeveloperIdAsync(developerIdToLogout.LoginId).GetAwaiter();
            DeveloperIds?.Remove(developerIdToLogout);
        }

        try
        {
            Changed?.Invoke(this, developerIdToLogout);
        }
        catch (Exception error)
        {
            _log.Error($"LoggedOut event signaling failed: {error}");
        }

        return new ProviderOperationResult(ProviderOperationStatus.Success, null, "The developer account has been logged out successfully", "LogoutDeveloperId succeeded");
    }

    public IEnumerable<DeveloperId> GetLoggedInDeveloperIdsInternal()
    {
        List<DeveloperId> iDeveloperIds = new();
        lock (_developerIdsLock)
        {
            iDeveloperIds.AddRange(DeveloperIds);
        }

        return iDeveloperIds;
    }

    // Convert devID to internal devID.
    public DeveloperId GetDeveloperIdInternal(IDeveloperId devId)
    {
        var devIds = GetInstance().GetLoggedInDeveloperIdsInternal();
        var devIdInternal = devIds.Where(i => i.LoginId.Equals(devId.LoginId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        return devIdInternal ?? throw new ArgumentException(devId.LoginId);
    }

    public DeveloperId? GetDeveloperIdFromAccountIdentifier(string loginId)
    {
        var devIds = GetInstance().GetLoggedInDeveloperIdsInternal();
        var devIdInternal = devIds.Where(i => i.LoginId.Equals(loginId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

        return devIdInternal;
    }

    // Internal Functions.
    private DeveloperId CreateOrUpdateDeveloperId(IAccount account)
    {
        // Query necessary data and populate Developer Id.
        DeveloperId newDeveloperId = new(account.Username, account.Username, account.Username, string.Empty);

        var duplicateDeveloperIds = DeveloperIds.Where(d => d.LoginId.Equals(newDeveloperId.LoginId, StringComparison.OrdinalIgnoreCase));

        if (duplicateDeveloperIds.Any())
        {
            _log.Information($"DeveloperID already exists! Updating accessToken");
            try
            {
                try
                {
                    Changed?.Invoke(this as IDeveloperIdProvider, duplicateDeveloperIds.Single() as IDeveloperId);
                }
                catch (Exception error)
                {
                    _log.Error($"Updated event signaling failed: {error}");
                }
            }
            catch (InvalidOperationException)
            {
                _log.Warning($"Multiple copies of same DeveloperID already exists");
                throw new InvalidOperationException("Multiple copies of same DeveloperID already exists");
            }
        }
        else
        {
            lock (_developerIdsLock)
            {
                DeveloperIds.Add(newDeveloperId);
            }

            try
            {
                Changed?.Invoke(this as IDeveloperIdProvider, newDeveloperId as IDeveloperId);
            }
            catch (Exception error)
            {
                _log.Error($"LoggedIn event signaling failed: {error}");
            }
        }

        return newDeveloperId;
    }

    private void RestoreDeveloperIds(Task<IEnumerable<string>> task)
    {
        var loginIds = task.Result;
        foreach (var loginId in loginIds)
        {
            DeveloperId developerId = new(loginId, loginId, loginId, string.Empty);

            lock (_developerIdsLock)
            {
                DeveloperIds.Add(developerId);
            }

            _log.Information($"Restored DeveloperId");
        }

        return;
    }

    internal void RefreshDeveloperId(IDeveloperId developerIdInternal)
    {
        Changed?.Invoke(this as IDeveloperIdProvider, developerIdInternal as IDeveloperId);
    }

    public AuthenticationExperienceKind GetAuthenticationExperienceKind()
    {
        return _authenticationExperienceForAzureExtension;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public AuthenticationState GetDeveloperIdState(IDeveloperId developerId)
    {
        DeveloperId? developerIdToFind;
        lock (_developerIdsLock)
        {
            developerIdToFind = DeveloperIds?.Find(e => e.LoginId == developerId.LoginId);
            if (developerIdToFind == null)
            {
                return AuthenticationState.LoggedOut;
            }
            else
            {
                return AuthenticationState.LoggedIn;
            }
        }
    }

    public AuthenticationResult? GetAuthenticationResultForDeveloperId(DeveloperId developerId)
    {
        try
        {
            var taskResult = DeveloperIdAuthenticationHelper.ObtainTokenForLoggedInDeveloperAccount(DeveloperIdAuthenticationHelper.MicrosoftEntraIdSettings.ScopesArray, developerId.LoginId);
            if (taskResult.Result != null)
            {
                return taskResult.Result;
            }
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

        return null;
    }

    public AdaptiveCardSessionResult GetLoginAdaptiveCardSession() => throw new NotImplementedException();
}
