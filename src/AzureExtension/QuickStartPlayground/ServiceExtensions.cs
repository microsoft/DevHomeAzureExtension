// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.QuickStartPlayground;

public static class ServiceExtensions
{
    public static IServiceCollection AddQuickStartPlayground(this IServiceCollection services)
    {
#if DEBUG
        services.AddSingleton<IQuickStartProjectProvider, AzureOpenAIDevContainerQuickStartProjectProvider>();
#endif
        services.AddSingleton<IQuickStartProjectProvider, OpenAIDevContainerQuickStartProjectProvider>();
        services.AddSingleton<AzureOpenAIServiceFactory>(sp => (openAIEndpoint) => ActivatorUtilities.CreateInstance<AzureOpenAIService>(sp, openAIEndpoint));
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<IAICredentialService, AICredentialService>();
        return services;
    }

    // Intentionally returning a list so that it is the type that is marshaled across process.
    public static List<IQuickStartProjectProvider> GetQuickStartProjectProviders(this IServiceProvider serviceProvider)
    {
        return new List<IQuickStartProjectProvider>(serviceProvider.GetServices<IQuickStartProjectProvider>());
    }
}
