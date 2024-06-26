// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Providers;

public sealed class SettingsProvider : ISettingsProvider
{
    private readonly IAICredentialService _aiCredentialService;

    public string DisplayName => Resources.GetResource(@"SettingsProviderDisplayName");

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
