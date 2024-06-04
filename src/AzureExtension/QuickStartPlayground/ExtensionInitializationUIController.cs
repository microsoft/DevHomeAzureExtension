// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace DevHomeAzureExtension.QuickStartPlayground;

// This class serves as a wrapper around multiple adaptive cards which are displayed one after
// another but don't know about each other.  After each adaptive card is done, the next is displayed.
public sealed class ExtensionInitializationUIController : IExtensionAdaptiveCardSession2
{
    private readonly QuickStartProjectAdaptiveCardResult[] _adaptiveCardsToDisplay;
    private int _currentCardIndex;
    private IExtensionAdaptiveCard? _extensionUI;

    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

    public ExtensionInitializationUIController(QuickStartProjectAdaptiveCardResult[] adaptiveCardsToDisplay)
    {
        _adaptiveCardsToDisplay = adaptiveCardsToDisplay;
        _currentCardIndex = 0;
        _extensionUI = null;
    }

    public static QuickStartProjectAdaptiveCardResult GetAdaptiveCard(QuickStartProjectAdaptiveCardResult[] adaptiveCardsToDisplay)
    {
        if (adaptiveCardsToDisplay.Length == 0)
        {
            return null!;
        }

        return new QuickStartProjectAdaptiveCardResult(new ExtensionInitializationUIController(adaptiveCardsToDisplay));
    }

    public void Dispose()
    {
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _extensionUI = extensionUI;
        return RenderAdaptiveCard();
    }

    private ProviderOperationResult RenderAdaptiveCard()
    {
        var adpativeCardResult = _adaptiveCardsToDisplay[_currentCardIndex];
        adpativeCardResult.AdaptiveCardSession.Stopped += AdaptiveCardSessionStopped;
        return adpativeCardResult.AdaptiveCardSession.Initialize(_extensionUI);
    }

    private void AdaptiveCardSessionStopped(IExtensionAdaptiveCardSession2 sender, ExtensionAdaptiveCardSessionStoppedEventArgs args)
    {
        _adaptiveCardsToDisplay[_currentCardIndex].AdaptiveCardSession.Stopped -= AdaptiveCardSessionStopped;

        // If adaptive card session failed, don't proceed to the next adaptive card.
        if (args.Result.Status != ProviderOperationStatus.Success)
        {
            Stopped?.Invoke(this, args);
            return;
        }

        _currentCardIndex++;
        if (_currentCardIndex == _adaptiveCardsToDisplay.Length)
        {
            // No more adaptive cards to display, invoke stopped to let Dev Home know.
            Stopped?.Invoke(this, args);
        }
        else
        {
            var result = RenderAdaptiveCard();
            if (result.Status != ProviderOperationStatus.Success)
            {
                Stopped?.Invoke(this, new(result, string.Empty));
            }
        }
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return _adaptiveCardsToDisplay[_currentCardIndex].AdaptiveCardSession.OnAction(action, inputs);
    }
}
