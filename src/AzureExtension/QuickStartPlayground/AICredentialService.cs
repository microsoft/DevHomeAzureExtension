// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using DevHomeAzureExtension.Contracts;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace DevHomeAzureExtension.QuickStartPlayground;

public sealed class AICredentialService : IAICredentialService
{
    private const string CredResourceNamePrefix = "AzureDevHomeExtension";

    private string AddCredentialResourceNamePrefix(string resource, string loginId) => $"{CredResourceNamePrefix}-{resource}: {loginId}";

    private string GetCredentialResourceName(string resource) => $"{CredResourceNamePrefix}-{resource}";

    public unsafe SecureString? GetCredentials(string resource, string loginId)
    {
        var credentialResourceName = GetCredentialResourceName(resource);
        var credentialNameToRetrieve = AddCredentialResourceNamePrefix(resource, loginId);

        var isCredentialRetrieved = PInvoke.CredRead(credentialNameToRetrieve, (uint)CRED_TYPE.CRED_TYPE_GENERIC, out var credential);
        try
        {
            if (!isCredentialRetrieved)
            {
                var error = Marshal.GetLastWin32Error();

                // NotFound is expected and can be ignored.
                if (error == (int)WIN32_ERROR.ERROR_NOT_FOUND)
                {
                    return null;
                }

                throw new Win32Exception(error);
            }

            if (credential->CredentialBlob is null)
            {
                throw new Win32Exception((int)WIN32_ERROR.ERROR_NOT_FOUND);
            }

            var secureAccessToken = new SecureString(
                (char*)credential->CredentialBlob,
                (int)(credential->CredentialBlobSize / UnicodeEncoding.CharSize));
            secureAccessToken.MakeReadOnly();
            return secureAccessToken;
        }
        finally
        {
            PInvoke.CredFree(credential);
        }
    }

    public void RemoveCredentials(string resource, string loginId)
    {
        var targetCredentialToDelete = AddCredentialResourceNamePrefix(resource, loginId);
        var isCredentialDeleted = PInvoke.CredDelete(targetCredentialToDelete, (uint)CRED_TYPE.CRED_TYPE_GENERIC);
        if (!isCredentialDeleted)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public unsafe void SaveCredentials(string resource, string loginId, SecureString? accessToken)
    {
        var credentialNameToRetrieve = AddCredentialResourceNamePrefix(resource, loginId);

        fixed (char* loginIdPtr = loginId)
        {
            fixed (char* credentialNameToRetrievePtr = credentialNameToRetrieve)
            {
                // Initialize a credential object.
                var credential = new CREDENTIALW
                {
                    Type = CRED_TYPE.CRED_TYPE_GENERIC,
                    TargetName = new PWSTR(credentialNameToRetrievePtr),
                    UserName = new PWSTR(loginIdPtr),
                    Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
                    AttributeCount = 0,
                    Flags = 0,
                    Comment = default,
                };

                try
                {
                    if (accessToken != null)
                    {
                        credential.CredentialBlob = (byte*)Marshal.SecureStringToCoTaskMemUnicode(accessToken);
                        credential.CredentialBlobSize = (uint)(accessToken.Length * UnicodeEncoding.CharSize);
                    }
                    else
                    {
                        throw new ArgumentNullException(nameof(accessToken));
                    }

                    // Store credential under Windows Credentials inside Credential Manager.
                    var isCredentialSaved = PInvoke.CredWrite(credential, 0);
                    if (!isCredentialSaved)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    if ((nint)credential.CredentialBlob != 0)
                    {
                        Marshal.FreeCoTaskMem((nint)credential.CredentialBlob);
                    }
                }
            }
        }
    }
}
