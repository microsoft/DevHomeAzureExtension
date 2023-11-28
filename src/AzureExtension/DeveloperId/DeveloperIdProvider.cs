// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Identity.Client;
using Microsoft.UI;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace DevHomeAzureExtension.DeveloperId;

public class DeveloperIdProvider : IDeveloperIdProvider
{
    // Locks to control access to Singleton class members.
    private static readonly object DeveloperIdsLock = new();

    private static readonly object AuthenticationProviderLock = new();

    // DeveloperId list containing all Logged in Ids.
    private List<DeveloperId> DeveloperIds
    {
        get; set;
    }

    private AuthenticationHelper DeveloperIdAuthenticationHelper
    {
        get; set;
    }

    // DeveloperIdProvider uses singleton pattern.
    private static DeveloperIdProvider? singletonDeveloperIdProvider;

    public event TypedEventHandler<IDeveloperIdProvider, IDeveloperId>? Changed;

    private readonly AuthenticationExperienceKind authenticationExperienceForAzureExtension = AuthenticationExperienceKind.CustomProvider;

    public string DisplayName => "Azure";

    // Private constructor for Singleton class.
    private DeveloperIdProvider()
    {
        Log.Logger()?.ReportInfo($"Creating DeveloperIdProvider singleton instance");

        lock (DeveloperIdsLock)
        {
            DeveloperIds ??= new List<DeveloperId>();

            DeveloperIdAuthenticationHelper = new AuthenticationHelper();

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
            Log.Logger()?.ReportInfo($"SSO For Azure Extension");
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
                    Log.Logger()?.ReportWarn($"DeveloperId not found to remediate");
                }
            }

            return new DeveloperIdsResult(developerIds);
        }

        return new DeveloperIdsResult(null, "No account remediation required");
    }

    public static DeveloperIdProvider GetInstance()
    {
        lock (AuthenticationProviderLock)
        {
            singletonDeveloperIdProvider ??= new DeveloperIdProvider();
        }

        return singletonDeveloperIdProvider;
    }

    public DeveloperIdsResult GetLoggedInDeveloperIds()
    {
        List<IDeveloperId> iDeveloperIds = new();
        lock (DeveloperIdsLock)
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
                Log.Logger()?.ReportError($"Invalid AuthRequest");
                var exception = new InvalidOperationException();
                return new DeveloperIdResult(exception, "An issue has occurred with the authentication request");
            }

            var devId = CreateOrUpdateDeveloperId(account.Result);
            Log.Logger()?.ReportInfo($"New DeveloperId logged in");
            return new DeveloperIdResult(devId);
        }).AsAsyncOperation();
    }

    public ProviderOperationResult LogoutDeveloperId(IDeveloperId developerId)
    {
        DeveloperId? developerIdToLogout;
        lock (DeveloperIdsLock)
        {
            developerIdToLogout = DeveloperIds?.Find(e => e.LoginId == developerId.LoginId);
            if (developerIdToLogout == null)
            {
                Log.Logger()?.ReportError($"Unable to find DeveloperId to logout");
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
            Log.Logger()?.ReportError($"LoggedOut event signaling failed: {error}");
        }

        return new ProviderOperationResult(ProviderOperationStatus.Success, null, "The developer account has been logged out successfully", "LogoutDeveloperId succeeded");
    }

    public IEnumerable<DeveloperId> GetLoggedInDeveloperIdsInternal()
    {
        List<DeveloperId> iDeveloperIds = new();
        lock (DeveloperIdsLock)
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
            Log.Logger()?.ReportInfo($"DeveloperID already exists! Updating accessToken");
            try
            {
                try
                {
                    Changed?.Invoke(this as IDeveloperIdProvider, duplicateDeveloperIds.Single() as IDeveloperId);
                }
                catch (Exception error)
                {
                    Log.Logger()?.ReportError($"Updated event signaling failed: {error}");
                }
            }
            catch (InvalidOperationException)
            {
                Log.Logger()?.ReportWarn($"Multiple copies of same DeveloperID already exists");
                throw new InvalidOperationException("Multiple copies of same DeveloperID already exists");
            }
        }
        else
        {
            lock (DeveloperIdsLock)
            {
                DeveloperIds.Add(newDeveloperId);
            }

            try
            {
                Changed?.Invoke(this as IDeveloperIdProvider, newDeveloperId as IDeveloperId);
            }
            catch (Exception error)
            {
                Log.Logger()?.ReportError($"LoggedIn event signaling failed: {error}");
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

            lock (DeveloperIdsLock)
            {
                DeveloperIds.Add(developerId);
            }

            Log.Logger()?.ReportInfo($"Restored DeveloperId");
        }

        return;
    }

    internal void RefreshDeveloperId(IDeveloperId developerIdInternal)
    {
        Changed?.Invoke(this as IDeveloperIdProvider, developerIdInternal as IDeveloperId);
    }

    public AuthenticationExperienceKind GetAuthenticationExperienceKind()
    {
        return authenticationExperienceForAzureExtension;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public AuthenticationState GetDeveloperIdState(IDeveloperId developerId)
    {
        DeveloperId? developerIdToFind;
        lock (DeveloperIdsLock)
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

        return null;
    }

    public AdaptiveCardSessionResult GetLoginAdaptiveCardSession() => throw new NotImplementedException();
}
