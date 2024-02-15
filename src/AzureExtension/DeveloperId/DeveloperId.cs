// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.DeveloperId;

public class DeveloperId : IDeveloperId
{
    public string LoginId { get; private set; }

    public string DisplayName { get; private set; }

    public string Email { get; private set; }

    public string Url { get; private set; }

    private Dictionary<Uri, VssConnection> Connections { get; } = new Dictionary<Uri, VssConnection>();

    public DeveloperId()
    {
        LoginId = string.Empty;
        DisplayName = string.Empty;
        Email = string.Empty;
        Url = string.Empty;
    }

    public DeveloperId(string loginId, string displayName, string email, string url)
    {
        LoginId = loginId;
        DisplayName = displayName;
        Email = email;
        Url = url;
    }

    ~DeveloperId()
    {
        LoginId = string.Empty;
        DisplayName = string.Empty;
        Email = string.Empty;
        Url = string.Empty;
        return;
    }

    // DeveloperIdInternal interfaces
    public VssCredentials? GetCredentials()
    {
        try
        {
            var authResult = DeveloperIdProvider.GetInstance().GetAuthenticationResultForDeveloperId(this) ?? throw new ArgumentException(this.LoginId);
            if (authResult != null)
            {
                return new VssAadCredential(new VssAadToken("Bearer", authResult.AccessToken));
            }
        }
        catch (MsalUiRequiredException ex)
        {
            Log.Logger()?.ReportError($"GetVssCredentials failed and requires user interaction {ex}");
            throw;
        }
        catch (MsalServiceException ex)
        {
            Log.Logger()?.ReportError($"GetVssCredentials failed with MSAL service error: {ex}");
            throw;
        }
        catch (MsalClientException ex)
        {
            Log.Logger()?.ReportError($"GetVssCredentials failed with MSAL client error: {ex}");
            throw;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"GetVssCredentials failed with error: {ex}");
            throw;
        }

        return null;
    }
}
