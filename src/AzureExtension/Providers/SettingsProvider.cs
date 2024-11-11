// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension.Providers;

public class SettingsProvider() : ISettingsProvider
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", nameof(SettingsProvider)));

    private static readonly ILogger _log = _logger.Value;

    string ISettingsProvider.DisplayName => Resources.GetResource(@"SettingsProviderDisplayName", _log);

    public AdaptiveCardSessionResult GetSettingsAdaptiveCardSession()
    {
        _log.Information($"GetSettingsAdaptiveCardSession");
        return new AdaptiveCardSessionResult(new SettingsUIController());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
