// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

// ARMTokenService is a service that provides an Azure Resource Manager (ARM) token.
public interface IArmTokenService
{
    public Task<string> GetTokenAsync(IDeveloperId? devId);
}
