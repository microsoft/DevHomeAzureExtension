// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.QuickStartPlayground;

 public sealed class DevContainerProjectFeedback : IQuickStartProjectResultFeedbackHandler
{
    public DevContainerProjectFeedback(string prompt)
    {
        Prompt = prompt;
    }

    private string Prompt { get; set; }

    public ProviderOperationResult ProvideFeedback(bool isPositive, string feedback)
    {
        return new ProviderOperationResult(ProviderOperationStatus.Success, null, string.Empty, string.Empty);
    }
}
