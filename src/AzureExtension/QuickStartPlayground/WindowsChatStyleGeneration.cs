// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.QuickStartPlayground;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using Windows.Storage;
using static System.Net.Mime.MediaTypeNames;

namespace AzureExtension.QuickStartPlayground;

public sealed class WindowsChatStyleGeneration : IQuickStartChatStyleGenerationOperation
{
    private readonly string _prompt;
    private readonly IAzureOpenAIService _azureOpenAIService;

    public WindowsChatStyleGeneration(
        string prompt,
        IAzureOpenAIService azureOpenAIService)
    {
        _prompt = prompt;
        _azureOpenAIService = azureOpenAIService;
    }

    public IAsyncOperation<QuickStartChatStyleResult> GenerateChatStyleResponse()
    {
        return AsyncInfo.Run<QuickStartChatStyleResult>(async (cancellationToken) =>
        {
            var validateProjectType = await Completions.CheckPromptProjectType(_azureOpenAIService, _prompt);
            if (validateProjectType.Contains(Resources.GetResource(@"QuickstartPlaground_ChatStyle_NotValid")))
            {
                return new QuickStartChatStyleResult(validateProjectType);
            }

            var validateClearObjective = await Completions.CheckPromptClearObjective(_azureOpenAIService, _prompt);
            if (validateClearObjective.Contains(Resources.GetResource(@"QuickstartPlaground_ChatStyle_NotValid")))
            {
                return new QuickStartChatStyleResult(validateClearObjective);
            }

            var validateDetailedRequirements = await Completions.CheckPromptDetailedRequirements(_azureOpenAIService, _prompt);
            if (validateDetailedRequirements.Contains(Resources.GetResource(@"QuickstartPlaground_ChatStyle_NotValid")))
            {
                return new QuickStartChatStyleResult(validateDetailedRequirements);
            }

            var validateLanguageRequirements = await Completions.CheckPromptLanguageRequirements(_azureOpenAIService, _prompt);
            if (validateLanguageRequirements.Contains(Resources.GetResource(@"QuickstartPlaground_ChatStyle_NotValid")))
            {
                return new QuickStartChatStyleResult(validateLanguageRequirements);
            }

            var something = Resources.GetResource(@"QuickstartPlaground_ChatStyle_ValidPrompt");

            return new QuickStartChatStyleResult(Resources.GetResource(@"QuickstartPlaground_ChatStyle_ValidPrompt"));
        });
    }
}
