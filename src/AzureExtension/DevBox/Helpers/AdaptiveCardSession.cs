// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Helpers;

public class AdaptiveCardSession : IExtensionAdaptiveCardSession2, IDisposable
{
    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs> Stopped = (s, e) => { };

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI) => throw new NotImplementedException();

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs) => throw new NotImplementedException();
}
