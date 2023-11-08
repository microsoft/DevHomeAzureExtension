// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Windows.Storage;

namespace DevHomeAzureExtension.DeveloperId;

public class AuthenticationSettings
{
    private readonly string cacheFolderPathDefault = Path.Combine(Path.GetTempPath(), "AzureExtension");
    private string? cacheFolderPath;

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
        get => cacheFolderPath is null ? cacheFolderPathDefault : cacheFolderPath;
        private set => cacheFolderPath = string.IsNullOrEmpty(value) ? cacheFolderPathDefault : value;
    }

    public string Scopes
    {
        get; private set;
    }

    public string[] ScopesArray => Scopes.Split(' ');

    public AuthenticationSettings()
    {
        Authority = "https://login.microsoftonline.com/organizations";
        ClientId = "318b152a-4c0e-4050-879d-5e98031c4ccf";
        TenantId = string.Empty;
        RedirectURI = "ms-appx-web://microsoft.aad.brokerplugin/{0}";
        CacheFileName = "msal_cache";
        CacheDir = ApplicationData.Current != null ? ApplicationData.Current.LocalFolder.Path : cacheFolderPathDefault;
        Scopes = "499b84ac-1321-427f-aa17-267ca6975798/user_impersonation";
    }
}
