﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Providers;

public sealed class SettingsProvider : ISettingsProvider
{
    private readonly IAICredentialService _aiCredentialService;

    public string DisplayName => "Test Settings";

    public SettingsProvider(IAICredentialService aiCredentialService)
    {
        _aiCredentialService = aiCredentialService;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public AdaptiveCardSessionResult GetSettingsAdaptiveCardSession()
    {
        return new AdaptiveCardSessionResult(new SettingsUIController(_aiCredentialService));
    }
}
