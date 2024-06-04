// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Storage;

namespace DevHomeAzureExtension.QuickStartPlayground;

public abstract class DevContainerQuickStartProjectProvider : IQuickStartProjectProvider
{
    private readonly IAzureOpenAIService _azureOpenAIService;
    private readonly IInstalledAppsService _installedAppsService;
    private readonly string _displayName;
    private readonly Uri _privacyPolicyUri;
    private readonly Uri _termsOfServiceUri;

    public DevContainerQuickStartProjectProvider(
        IAzureOpenAIService azureOpenAIService,
        IInstalledAppsService installedAppsService,
        string displayName,
        Uri privacyUri,
        Uri termsUri)
    {
        _azureOpenAIService = azureOpenAIService;
        _installedAppsService = installedAppsService;
        _displayName = displayName;
        _privacyPolicyUri = privacyUri;
        _termsOfServiceUri = termsUri;
    }

    protected IAzureOpenAIService AzureOpenAIService => _azureOpenAIService;

    public string DisplayName => _displayName;

    public Uri PrivacyPolicyUri => _privacyPolicyUri;

    public Uri TermsOfServiceUri => _termsOfServiceUri;

    public string[] SamplePrompts =>
    [
        Resources.GetResource(@"QuickstartPlayground_ExamplePrompt1"),
        Resources.GetResource(@"QuickstartPlayground_ExamplePrompt2"),
        Resources.GetResource(@"QuickstartPlayground_ExamplePrompt3"),
    ];

    public abstract QuickStartProjectAdaptiveCardResult CreateAdaptiveCardSessionForAIProviderInitialization();

    public QuickStartProjectAdaptiveCardResult CreateAdaptiveCardSessionForExtensionInitialization()
    {
        var adaptiveCards = new List<QuickStartProjectAdaptiveCardResult>();

        var dependencyAdaptiveCard = DependencyUIController.GetAdaptiveCard(_installedAppsService);
        if (dependencyAdaptiveCard != null)
        {
            adaptiveCards.Add(dependencyAdaptiveCard);
        }

        var aiProviderInitializationAdaptiveCard = CreateAdaptiveCardSessionForAIProviderInitialization();
        if (aiProviderInitializationAdaptiveCard != null)
        {
            adaptiveCards.Add(aiProviderInitializationAdaptiveCard);
        }

        return ExtensionInitializationUIController.GetAdaptiveCard([.. adaptiveCards]);
    }

    public IQuickStartProjectGenerationOperation CreateProjectGenerationOperation(string prompt, StorageFolder outputFolder)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        return new DevContainerGenerationOperation(prompt, outputFolder, _azureOpenAIService, _installedAppsService);
    }
}
