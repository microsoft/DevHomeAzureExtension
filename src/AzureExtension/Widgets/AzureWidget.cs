// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Widgets.Enums;
using Microsoft.Windows.DevHome.SDK;
using Microsoft.Windows.Widgets.Providers;

namespace DevHomeAzureExtension.Widgets;

public abstract class AzureWidget : WidgetImpl
{
    protected static readonly TimeSpan WidgetDataRequestMinTime = TimeSpan.FromSeconds(30);
    protected static readonly TimeSpan WidgetRefreshRate = TimeSpan.FromMinutes(5);
    protected static readonly string EmptyJson = new JsonObject().ToJsonString();

    protected IAzureDataManager? DataManager { get; private set; }

    private DateTime lastUpdateRequest = DateTime.MinValue;

    protected static readonly string Name = nameof(AzureWidget);

    protected WidgetActivityState ActivityState { get; set; } = WidgetActivityState.Unknown;

    protected WidgetDataState DataState { get; set; } = WidgetDataState.Unknown;

    protected string DataErrorMessage { get; set; } = string.Empty;

    protected WidgetPageState Page { get; set; } = WidgetPageState.Unknown;

    protected string ContentData { get; set; } = EmptyJson;

    protected bool CanPin
    {
        get; set;
    }

    protected bool Pinned
    {
        get; set;
    }

    protected bool Enabled
    {
        get; set;
    }

    protected Dictionary<WidgetPageState, string> Template { get; set; } = new();

    protected string ConfigurationData
    {
        get => State();

        set => SetState(value);
    }

    protected string SavedConfigurationData { get; set; } = string.Empty;

    protected DateTime LastUpdated { get; set; } = DateTime.MinValue;

    protected DataUpdater? DataUpdater { get; private set; }

    public AzureWidget()
    {
        // Widgets are created with the factory pattern, so initialization happens in Create.
    }

    ~AzureWidget()
    {
        try
        {
            DataUpdater?.Dispose();
            DataManager?.Dispose();
            AzureDataManager.OnUpdate -= HandleDataManagerUpdate;
            DeveloperIdProvider.GetInstance().Changed -= HandleDeveloperIdChange;
        }
        catch
        {
            // Best effort.
        }
    }

    public abstract void RequestContentData();

    public abstract void LoadContentData();

    public abstract void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e);

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        Id = widgetContext.Id;
        Enabled = widgetContext.IsActive;
        ConfigurationData = state;

        DataManager = AzureDataManager.CreateInstance(ShortId) ?? throw new DataStoreInaccessibleException();
        DataUpdater = new DataUpdater(PeriodicUpdate);
        AzureDataManager.OnUpdate += HandleDataManagerUpdate;
        DeveloperIdProvider.GetInstance().Changed += HandleDeveloperIdChange;

        // If there is a state, it is being retrieved from the widget service, so
        // this widget was pinned before.
        if (state.Any())
        {
            Pinned = true;
            CanPin = true;
        }

        UpdateActivityState();
    }

    public override void Activate(WidgetContext widgetContext)
    {
        Enabled = true;
        UpdateActivityState();
    }

    public override void Deactivate(string widgetId)
    {
        Enabled = false;
        UpdateActivityState();
    }

    public override void DeleteWidget(string widgetId, string customState)
    {
        Enabled = false;
        SetDeleted();
    }

    public override void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        Enabled = contextChangedArgs.WidgetContext.IsActive;

        // The first time the widget changes context is when leaving the Add Widget dialog
        // and goes to the actual dashboard when the user clicks the Pin button.
        if (CanPin && !Pinned)
        {
            Pinned = true;
            Page = WidgetPageState.Loading;
            UpdateWidget();
        }

        UpdateActivityState();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);
        Log.Logger()?.ReportDebug(Name, ShortId, $"ActionInvoked: {verb}");

        switch (verb)
        {
            case WidgetAction.Submit:
                HandleSubmit(actionInvokedArgs);
                break;

            case WidgetAction.SignIn:
                _ = HandleSignIn();
                break;

            case WidgetAction.Save:
                SavedConfigurationData = string.Empty;
                ContentData = EmptyJson;
                DataState = WidgetDataState.Unknown;
                SetActive();
                break;

            case WidgetAction.Cancel:
                ConfigurationData = SavedConfigurationData;
                SavedConfigurationData = string.Empty;
                CanPin = true;
                SetActive();
                break;

            case WidgetAction.Unknown:
                Log.Logger()?.ReportError(Name, ShortId, $"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }
    }

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        SavedConfigurationData = ConfigurationData;
        SetConfigure();
    }

    protected abstract void HandleSubmit(WidgetActionInvokedArgs args);

    // Expect sign-in to be async, but since it is not yet implemented, suppressing this warning.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected async Task HandleSignIn()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        Log.Logger()?.ReportInfo(Name, ShortId, $"WidgetAction invoked for user sign in");

        // Do Sign-in Flow
        // var authProvider = DeveloperIdProvider.GetInstance();
        UpdateActivityState();
        Log.Logger()?.ReportInfo(Name, ShortId, $"User sign in successful from WidgetAction invocation");
    }

    protected WidgetAction GetWidgetActionForVerb(string verb)
    {
        try
        {
            return Enum.Parse<WidgetAction>(verb);
        }
        catch (Exception)
        {
            // Invalid verb.
            Log.Logger()?.ReportError(Name, ShortId, $"Unknown WidgetAction verb: {verb}");
            return WidgetAction.Unknown;
        }
    }

    public abstract string GetConfiguration(string data);

    public string GetSignIn()
    {
        var signInData = new JsonObject
        {
            { "message", Resources.GetResource(@"Widget_Template/SignInRequired", Log.Logger()) },
            { "configuring", true },
        };

        return signInData.ToString();
    }

    public bool IsUserLoggedIn()
    {
        var authProvider = DeveloperIdProvider.GetInstance();
        return authProvider.GetLoggedInDeveloperIds().DeveloperIds.Any();
    }

    protected DeveloperId.DeveloperId? GetDevId(string login)
    {
        var devIdProvider = DeveloperIdProvider.GetInstance();
        DeveloperId.DeveloperId? developerId = null;

        foreach (var devId in devIdProvider.GetLoggedInDeveloperIds().DeveloperIds)
        {
            if (devId.LoginId == login)
            {
                developerId = (DeveloperId.DeveloperId)devId;
            }
        }

        return developerId;
    }

    public void UpdateActivityState()
    {
        if (ActivityState == WidgetActivityState.Deleted)
        {
            Log.Logger()?.ReportError(Name, ShortId, $"Attempted to change state of deleted widget: {Id}");
            return;
        }

        // State logic for the Widget:
        // Signed in -> Valid Repository Url -> Active / Inactive per widget host.
        if (!IsUserLoggedIn())
        {
            SetSignIn();
            return;
        }

        if (!Pinned || !CanPin)
        {
            SetConfigure();
            return;
        }

        if (Enabled)
        {
            SetActive();
            return;
        }

        SetInactive();
    }

    public void UpdateWidget()
    {
        WidgetUpdateRequestOptions updateOptions = new(Id)
        {
            Data = GetData(Page),
            Template = GetTemplateForPage(Page),
            CustomState = ConfigurationData,
        };

        Log.Logger()?.ReportDebug(Name, ShortId, $"Updating widget for {Page}");
        WidgetManager.GetDefault().UpdateWidget(updateOptions);
    }

    public virtual string GetTemplatePath(WidgetPageState page)
    {
        return string.Empty;
    }

    public virtual string GetData(WidgetPageState page)
    {
        return string.Empty;
    }

    protected string GetTemplateForPage(WidgetPageState page)
    {
        if (Template.ContainsKey(page))
        {
            Log.Logger()?.ReportDebug(Name, ShortId, $"Using cached template for {page}");
            return Template[page];
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, GetTemplatePath(page));
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
            template = Resources.ReplaceIdentifiers(template, Resources.GetWidgetResourceIdentifiers(), Log.Logger());
            Log.Logger()?.ReportDebug(Name, ShortId, $"Caching template for {page}");
            Template[page] = template;
            return template;
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError(Name, ShortId, "Error getting template.", e);
            return string.Empty;
        }
    }

    protected string GetCurrentState()
    {
        return $"State: {ActivityState}  Page: {Page}  Data: {DataState}";
    }

    protected void LogCurrentState()
    {
        Log.Logger()?.ReportDebug(Name, ShortId, GetCurrentState());
    }

    protected void SetActive()
    {
        ActivityState = WidgetActivityState.Active;
        Page = WidgetPageState.Content;

        if (ContentData == EmptyJson)
        {
            LoadContentData();
        }

        _ = DataUpdater?.Start();
        LogCurrentState();
        UpdateWidget();
    }

    protected void SetLoading()
    {
        ActivityState = WidgetActivityState.Active;
        Page = WidgetPageState.Loading;

        _ = DataUpdater?.Start();
        LogCurrentState();
        UpdateWidget();
    }

    protected void SetInactive()
    {
        ActivityState = WidgetActivityState.Inactive;
        DataUpdater?.Stop();

        // No need to update when we are inactive.
        LogCurrentState();
    }

    protected void SetConfigure()
    {
        // If moving to configure, reset the throttle so when we update to Active, the first update
        // will not get throttled.
        DataUpdater?.Stop();
        lastUpdateRequest = DateTime.MinValue;
        ActivityState = WidgetActivityState.Configure;
        Page = WidgetPageState.Configure;
        LogCurrentState();
        UpdateWidget();
    }

    protected void SetSignIn()
    {
        Page = WidgetPageState.SignIn;
        ActivityState = WidgetActivityState.SignIn;
        LogCurrentState();
        UpdateWidget();
    }

    private void SetDeleted()
    {
        // If this widget is deleted, disable the data updater, clear the state, set to Unknown.
        try
        {
            AzureDataManager.OnUpdate -= HandleDataManagerUpdate;
            DeveloperIdProvider.GetInstance().Changed -= HandleDeveloperIdChange;
            DataUpdater?.Stop();
            DataUpdater?.Dispose();
            DataManager?.Dispose();
            DataState = WidgetDataState.Disposed;
            DataUpdater = null;
            DataManager = null;
        }
        catch
        {
            // Best effort.
        }

        SetState(string.Empty);
        Page = WidgetPageState.Unknown;
        ActivityState = WidgetActivityState.Deleted;

        // Report deletion confirmation on the Info channel.
        Log.Logger()?.ReportInfo(Name, ShortId, GetCurrentState());
    }

    protected static string GetMessageForError(ErrorType errorType, string? fallback = null)
    {
        var identifier = $"ErrorMessage/{errorType}";
        var message = errorType switch
        {
            ErrorType.None => string.Empty,
            _ => Resources.GetResource(identifier, Log.Logger() ?? null),
        };

        // If identifier and message are different, then it means we have a specific message to
        // display for this error.
        if (message != identifier)
        {
            return message;
        }

        // Otherwise we do not have a specific error defined and should use the fallback if it
        // exists. If no fallback use the name of the error type.
        return fallback ?? errorType.ToString();
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task PeriodicUpdate()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        // Only update per the update interval.
        // This is intended to be dynamic in the future.
        if (DateTime.Now - lastUpdateRequest < WidgetRefreshRate)
        {
            return;
        }

        if (ActivityState == WidgetActivityState.Configure)
        {
            return;
        }

        try
        {
            RequestContentData();
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(Name, ShortId, "Failed Requesting Update", ex);
        }

        lastUpdateRequest = DateTime.Now;
    }

    private void HandleDeveloperIdChange(object? sender, IDeveloperId e)
    {
        Log.Logger()?.ReportInfo(Name, ShortId, $"Change in Developer Id,  Updating widget state.");
        UpdateActivityState();
    }
}
