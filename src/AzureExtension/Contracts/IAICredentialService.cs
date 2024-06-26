// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;

namespace DevHomeAzureExtension.Contracts;

public interface IAICredentialService
{
    SecureString? GetCredentials(string resource, string loginId);

    void RemoveCredentials(string resource, string loginId);

    void SaveCredentials(string resource, string loginId, SecureString? accessToken);
}
