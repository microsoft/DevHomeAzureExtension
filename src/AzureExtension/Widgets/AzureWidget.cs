// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Widgets.Enums;
using Microsoft.Windows.DevHome.SDK;
using Microsoft.Windows.Widgets.Providers;
using Serilog;

namespace DevHomeAzureExtension.Widgets;

public abstract class AzureWidget : WidgetImpl
{
    protected static readonly TimeSpan WidgetDataRequestMinTime = TimeSpan.FromSeconds(30);
    protected static readonly TimeSpan WidgetRefreshRate = TimeSpan.FromMinutes(5);
    protected static readonly string EmptyJson = new JsonObject().ToJsonString();

    protected IAzureDataManager? DataManager { get; private set; }

    private DateTime _lastUpdateRequest = DateTime.MinValue;

    protected WidgetActivityState ActivityState { get; set; } = WidgetActivityState.Unknown;

    protected WidgetDataState DataState { get; set; } = WidgetDataState.Unknown;

    protected string DataErrorMessage { get; set; } = string.Empty;

    protected WidgetPageState Page { get; set; } = WidgetPageState.Unknown;

    protected string ContentData { get; set; } = EmptyJson;

    protected string LoadingMessage { get; set; } = string.Empty;

    protected string DeveloperLoginId { get; set; } = string.Empty;

    protected bool DeveloperIdLoginRequired { get; set; } = true;

    protected bool LoadedDataSuccessfully { get; set; }

    protected bool CanSave
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
        if (state.Length != 0)
        {
            Pinned = true;
            CanSave = true;
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

        UpdateActivityState();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);
        Log.Debug($"ActionInvoked: {verb}");

        switch (verb)
        {
            case WidgetAction.SignIn:
                _ = HandleSignIn();
                break;

            case WidgetAction.Save:
                if (ValidateConfiguration(actionInvokedArgs))
                {
                    SavedConfigurationData = string.Empty;
                    ContentData = EmptyJson;
                    DataState = WidgetDataState.Unknown;
                    LoadedDataSuccessfully = false;
                    SetActive();
                }

                break;

            case WidgetAction.Cancel:
                // Put back the configuration data from before we started changing the widget.
                ConfigurationData = SavedConfigurationData;
                SavedConfigurationData = string.Empty;
                CanSave = true;

                // Fields were updated on Save, restore them from the saved configuration data.
                ResetDataFromState(ConfigurationData);

                SetActive();
                break;

            case WidgetAction.Unknown:
                Log.Error($"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }
    }

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        // Set CanSave to false so user will have to Submit again before Saving.
        CanSave = false;
        SavedConfigurationData = ConfigurationData;
        SetConfigure();
    }

    protected abstract bool ValidateConfiguration(WidgetActionInvokedArgs args);

    protected abstract void ResetDataFromState(string state);

    // Expect sign-in to be async, but since it is not yet implemented, suppressing this warning.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected async Task HandleSignIn()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        Log.Information($"WidgetAction invoked for user sign in");

        // We cannot do sign-in flow because it requires the main window of DevHome.
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
            Log.Error($"Unknown WidgetAction verb: {verb}");
            return WidgetAction.Unknown;
        }
    }

    public abstract string GetConfiguration(string data);

    public string GetSignIn()
    {
        var signInData = new JsonObject
        {
            { "message", Resources.GetResource(@"Widget_Template/SignInRequired", Log) },
        };

        return signInData.ToString();
    }

    protected virtual bool IsUserLoggedIn()
    {
        // User is not logged in if either there are zero DeveloperIds logged in, or the selected
        // DeveloperId for this widget is not logged in.
        var authProvider = DeveloperIdProvider.GetInstance();
        if (!authProvider.GetLoggedInDeveloperIds().DeveloperIds.Any())
        {
            return false;
        }

        if (!DeveloperIdLoginRequired)
        {
            // At least one user is logged in, and this widget does not require a specific
            // DeveloperId so we are in a good state.
            return true;
        }

        if (string.IsNullOrEmpty(DeveloperLoginId))
        {
            // User has not yet chosen a DeveloperId, but there is at least one available, so the
            // user has logged in and we are in a good state.
            return true;
        }

        if (GetDevId(DeveloperLoginId) is not null)
        {
            // The selected DeveloperId is logged in so we are in a good state.
            return true;
        }

        return false;
    }

    public virtual string GetLoadingMessage()
    {
        if (string.IsNullOrEmpty(LoadingMessage))
        {
            return EmptyJson;
        }

        return new JsonObject
        {
            { "loadingMessage", LoadingMessage },
        }.ToJsonString();
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
            Log.Error($"Attempted to change state of deleted widget: {Id}");
            return;
        }

        // State logic for the Widget:
        // Signed in -> Valid Repository Url -> Active / Inactive per widget host.
        if (!IsUserLoggedIn())
        {
            SetSignIn();
            return;
        }

        if (SupportsCustomization && (!Pinned || !CanSave))
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

        Log.Debug($"Updating widget for {Page}");
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
        if (Template.TryGetValue(page, out var azureTemplate))
        {
            Log.Debug($"Using cached template for {page}");
            return azureTemplate;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, GetTemplatePath(page));
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);
            template = Resources.ReplaceIdentifiers(template, Resources.GetWidgetResourceIdentifiers(), Log);
            Log.Debug($"Caching template for {page}");
            Template[page] = template;
            return template;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error getting template.");
            return string.Empty;
        }
    }

    protected string GetCurrentState()
    {
        return $"State: {ActivityState}  Page: {Page}  Data: {DataState}";
    }

    protected void LogCurrentState()
    {
        Log.Debug(GetCurrentState());
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
        _lastUpdateRequest = DateTime.MinValue;
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
        Log.Information(GetCurrentState());
    }

    protected string GetMessageForError(ErrorType errorType, string? fallback = null)
    {
        var identifier = $"ErrorMessage/{errorType}";
        var message = errorType switch
        {
            ErrorType.None => string.Empty,
            _ => Resources.GetResource(identifier, Log),
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
        if (DateTime.UtcNow - _lastUpdateRequest < WidgetRefreshRate)
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
            Log.Error(ex, "Failed Requesting Update");
        }

        _lastUpdateRequest = DateTime.UtcNow;
    }

    // This method will attempt to select a DeveloperId if one is not already selected. By default
    // this will simply select the first DeveloperId in the list of available DeveloperIds.
    protected virtual void SetDefaultDeveloperLoginId()
    {
        if (!string.IsNullOrEmpty(DeveloperLoginId))
        {
            return;
        }

        var devIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIds().DeveloperIds;
        if (devIds is null)
        {
            return;
        }

        // Set as the first DevId found, unless we find a better match from the Url.
        DeveloperLoginId = devIds.FirstOrDefault()?.LoginId ?? string.Empty;
    }

    protected void HandleDeveloperIdChange(object? sender, IDeveloperId e)
    {
        if (e.LoginId == DeveloperLoginId)
        {
            try
            {
                var state = DeveloperIdProvider.GetInstance().GetDeveloperIdState(e);
                if (state == AuthenticationState.LoggedIn)
                {
                    // If the DevId we are set for logs in, refresh the data.
                    RequestContentData();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed getting DeveloperId state.");
            }
        }

        // Update state regardless, since we could be waiting for any developer to sign in.
        Log.Information($"Change in Developer Id,  Updating widget state.");
        UpdateActivityState();
    }
}
