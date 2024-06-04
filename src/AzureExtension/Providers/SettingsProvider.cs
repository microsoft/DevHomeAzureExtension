// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension.Providers;

public class SettingsProvider(IAICredentialService aiCredentialService) : ISettingsProvider
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", nameof(SettingsProvider)));

    private static readonly ILogger Log = _log.Value;

    string ISettingsProvider.DisplayName => Resources.GetResource(@"SettingsProviderDisplayName", Log);

    private readonly IAICredentialService _aiCredentialService = aiCredentialService;

    public AdaptiveCardSessionResult GetSettingsAdaptiveCardSession()
    {
        Log.Information($"GetSettingsAdaptiveCardSession");
        return new AdaptiveCardSessionResult(new SettingsUIController(_aiCredentialService));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
