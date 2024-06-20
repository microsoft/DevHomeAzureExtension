// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Windows.Storage;

namespace DevHomeAzureExtension.DeveloperId;

public class AuthenticationSettings
{
    private readonly string _cacheFolderPathDefault = Path.Combine(Path.GetTempPath(), "AzureExtension");
    private string? _cacheFolderPath;

    public string Authority
    {
        get; private set;
    }

    public string ClientId
    {
        get; private set;
    }

    public string TenantId
    {
        get; private set;
    }

    public string RedirectURI
    {
        get; private set;
    }

    public string CacheFileName
    {
        get; private set;
    }

    public string CacheDir
    {
        get => _cacheFolderPath is null ? _cacheFolderPathDefault : _cacheFolderPath;
        private set => _cacheFolderPath = string.IsNullOrEmpty(value) ? _cacheFolderPathDefault : value;
    }

    public string Scopes
    {
        get; private set;
    }

    public string[] ScopesArray => Scopes.Split(' ');

    public AuthenticationSettings()
    {
        Authority = string.Empty;
        ClientId = string.Empty;
        TenantId = string.Empty;
        RedirectURI = string.Empty;
        CacheFileName = string.Empty;
        CacheDir = string.Empty;
        Scopes = string.Empty;
    }

    public void InitializeSettings()
    {
        Authority = "https://login.microsoftonline.com/organizations";
        ClientId = "318b152a-4c0e-4050-879d-5e98031c4ccf";
        TenantId = string.Empty;
        RedirectURI = "ms-appx-web://microsoft.aad.brokerplugin/{0}";
        CacheFileName = "msal_cache";
        CacheDir = ApplicationData.Current != null ? ApplicationData.Current.LocalFolder.Path : _cacheFolderPathDefault;
        Scopes = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    }
}
