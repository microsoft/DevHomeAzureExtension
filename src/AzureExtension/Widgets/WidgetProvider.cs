// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using Serilog;

namespace DevHomeAzureExtension.Widgets;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("B91B13BB-B3B4-4F2E-9EF9-554757F33E1C")]
public sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(WidgetProvider));

    public WidgetProvider()
    {
        _log.Debug("Provider Constructed");
        widgetDefinitionRegistry.Add("Azure_QueryList", new WidgetImplFactory<AzureQueryListWidget>());
        widgetDefinitionRegistry.Add("Azure_QueryTiles", new WidgetImplFactory<AzureQueryTilesWidget>());
        widgetDefinitionRegistry.Add("Azure_PullRequests", new WidgetImplFactory<AzurePullRequestsWidget>());
        RecoverRunningWidgets();
    }

    private readonly Dictionary<string, IWidgetImplFactory> widgetDefinitionRegistry = new();
    private readonly Dictionary<string, WidgetImpl> runningWidgets = new();

    private void InitializeWidget(WidgetContext widgetContext, string state)
    {
        var widgetId = widgetContext.Id;
        var widgetDefinitionId = widgetContext.DefinitionId;
        _log.Debug($"Calling Initialize for Widget Id: {widgetId} - {widgetDefinitionId}");
        if (widgetDefinitionRegistry.TryGetValue(widgetDefinitionId, out var widgetFactory))
        {
            if (!runningWidgets.ContainsKey(widgetId))
            {
                var widgetImpl = widgetFactory.Create(widgetContext, state);
                runningWidgets.Add(widgetId, widgetImpl);
            }
            else
            {
                _log.Warning($"Attempted to initialize a widget twice: {widgetDefinitionId} - {widgetId}");
            }
        }
        else
        {
            _log.Error($"Unknown widget DefinitionId: {widgetDefinitionId}");
        }
    }

    private void RecoverRunningWidgets()
    {
        WidgetInfo[] runningWidgets;
        try
        {
            runningWidgets = WidgetManager.GetDefault().GetWidgetInfos();
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed retrieving list of running widgets.");
            return;
        }

        if (runningWidgets is null)
        {
            _log.Debug("No running widgets to recover.");
            return;
        }

        foreach (var widgetInfo in runningWidgets)
        {
            if (!this.runningWidgets.ContainsKey(widgetInfo.WidgetContext.Id))
            {
                InitializeWidget(widgetInfo.WidgetContext, widgetInfo.CustomState);
            }
        }

        _log.Debug("Finished recovering widgets.");
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        _log.Information($"CreateWidget id: {widgetContext.Id} definitionId: {widgetContext.DefinitionId}");
        InitializeWidget(widgetContext, string.Empty);
    }

    public void Activate(WidgetContext widgetContext)
    {
        _log.Debug($"Activate id: {widgetContext.Id} definitionId: {widgetContext.DefinitionId}");
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var widgetImpl))
        {
            widgetImpl.Activate(widgetContext);
        }
        else
        {
            // Called to activate a widget that we don't know about, which is unexpected. Try to recover by creating it.
            _log.Warning($"Found WidgetId that was not known: {widgetContext.Id}, attempting to recover by creating it.");
            CreateWidget(widgetContext);
            if (runningWidgets.TryGetValue(widgetId, out var widgetImplForUnknownWidget))
            {
                widgetImplForUnknownWidget.Activate(widgetContext);
            }
        }
    }

    public void Deactivate(string widgetId)
    {
        if (runningWidgets.TryGetValue(widgetId, out var widgetToDeactivate))
        {
            _log.Debug($"Deactivate id: {widgetId}");
            widgetToDeactivate.Deactivate(widgetId);
        }
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        if (runningWidgets.TryGetValue(widgetId, out var widgetToDelete))
        {
            _log.Information($"DeleteWidget id: {widgetId}");
            widgetToDelete.DeleteWidget(widgetId, customState);
            runningWidgets.Remove(widgetId);
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        _log.Debug($"OnActionInvoked id: {actionInvokedArgs.WidgetContext.Id} definitionId: {actionInvokedArgs.WidgetContext.DefinitionId}");
        var widgetContext = actionInvokedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var widgetToInvokeAnActionOn))
        {
            widgetToInvokeAnActionOn.OnActionInvoked(actionInvokedArgs);
        }
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        _log.Debug($"OnCustomizationRequested id: {customizationRequestedArgs.WidgetContext.Id} definitionId: {customizationRequestedArgs.WidgetContext.DefinitionId}");
        var widgetContext = customizationRequestedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var widgetToCustomize))
        {
            widgetToCustomize.OnCustomizationRequested(customizationRequestedArgs);
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        _log.Debug($"OnWidgetContextChanged id: {contextChangedArgs.WidgetContext.Id} definitionId: {contextChangedArgs.WidgetContext.DefinitionId}");
        var widgetContext = contextChangedArgs.WidgetContext;
        var widgetId = widgetContext.Id;
        if (runningWidgets.TryGetValue(widgetId, out var widgetWithAChangedContext))
        {
            widgetWithAChangedContext.OnWidgetContextChanged(contextChangedArgs);
        }
    }
}
