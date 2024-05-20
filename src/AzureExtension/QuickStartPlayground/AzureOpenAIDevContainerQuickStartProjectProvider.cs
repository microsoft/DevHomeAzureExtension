// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.QuickStartPlayground;

public sealed class AzureOpenAIDevContainerQuickStartProjectProvider : DevContainerQuickStartProjectProvider
{
    private static readonly Uri _privacyUri = new("https://privacy.microsoft.com/privacystatement");
    private static readonly Uri _termsUri = new("https://www.microsoft.com/legal/terms-of-use");

    public AzureOpenAIDevContainerQuickStartProjectProvider(AzureOpenAIServiceFactory azureOpenAIServiceFactory, IInstalledAppsService installedAppsService)
        : base(
            azureOpenAIServiceFactory(OpenAIEndpoint.AzureOpenAI),
            installedAppsService,
            Resources.GetResource(@"QuickStartProjectProviderUsingAzureOpenAIDisplayName"),
            _privacyUri,
            _termsUri)
    {
    }

    public override QuickStartProjectAdaptiveCardResult CreateAdaptiveCardSessionForAIProviderInitialization() => null!;
}
