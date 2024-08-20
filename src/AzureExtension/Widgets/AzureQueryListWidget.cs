// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.Widgets.Providers;
using Newtonsoft.Json;

namespace DevHomeAzureExtension.Widgets;

internal sealed class AzureQueryListWidget : AzureWidget
{
    private readonly string _sampleIconData = IconLoader.GetIconAsBase64("screenshot.png");

    // Widget Data
    private string _widgetTitle = string.Empty;
    private string _selectedQueryUrl = string.Empty;
    private string _selectedQueryId = string.Empty;
    private string? _message;

    // Creation and destruction methods
    public AzureQueryListWidget()
    : base()
    {
    }

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        if (state.Length != 0)
        {
            ResetDataFromState(state);
        }

        base.CreateWidget(widgetContext, state);
    }

    public override void DeleteWidget(string widgetId, string customState)
    {
        base.DeleteWidget(widgetId, customState);
    }

    // Helper methods
    private string GetIconForType(string? workItemType)
    {
        return workItemType switch
        {
            "Bug" => IconLoader.GetIconAsBase64("Bug.png"),
            "Feature" => IconLoader.GetIconAsBase64("Feature.png"),
            "Issue" => IconLoader.GetIconAsBase64("Issue.png"),
            "Impediment" => IconLoader.GetIconAsBase64("Impediment.png"),
            "Pull Request" => IconLoader.GetIconAsBase64("PullRequest.png"),
            "Task" => IconLoader.GetIconAsBase64("Task.png"),
            _ => IconLoader.GetIconAsBase64("ADO.png"),
        };
    }

    private string GetIconForStatusState(string? statusState)
    {
        return statusState switch
        {
            "Closed" or "Completed" => IconLoader.GetIconAsBase64("StatusGreen.png"),
            "Committed" or "Resolved" or "Started" => IconLoader.GetIconAsBase64("StatusBlue.png"),
            _ => IconLoader.GetIconAsBase64("StatusGray.png"),
        };
    }

    protected override bool ValidateConfiguration(WidgetActionInvokedArgs args)
    {
        // Set loading page while we fetch data from ADO.
        Page = WidgetPageState.Loading;
        UpdateWidget();

        CanSave = false;

        Page = WidgetPageState.Configure;
        var data = args.Data;
        var dataObject = JsonObject.Parse(data);
        _message = null;

        if (dataObject != null && dataObject["account"] != null && dataObject["query"] != null)
        {
            CanSave = false;
            _widgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
            _selectedQueryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
            DeveloperLoginId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
            SetDefaultDeveloperLoginId();
            if (DeveloperLoginId != dataObject["account"]?.GetValue<string>())
            {
                dataObject["account"] = DeveloperLoginId;
                data = dataObject.ToJsonString();
            }

            ConfigurationData = data;

            var developerId = GetDevId(DeveloperLoginId);
            if (developerId == null)
            {
                _message = Resources.GetResource(@"Widget_Template/DevIDError");
                UpdateActivityState();
                return false;
            }

            var queryInfo = AzureClientHelpers.GetQueryInfo(_selectedQueryUrl, developerId);
            _selectedQueryId = queryInfo.AzureUri.Query;   // This will be empty string if invalid query.
            if (queryInfo.Result != ResultType.Success)
            {
                _message = GetMessageForError(queryInfo.Error, queryInfo.ErrorMessage);
                UpdateActivityState();
                return false;
            }
            else
            {
                CanSave = true;
                Pinned = true;
                if (string.IsNullOrEmpty(_widgetTitle))
                {
                    _widgetTitle = queryInfo.Name;
                }

                Page = WidgetPageState.Content;
                UpdateActivityState();
                return true;
            }
        }

        return false;
    }

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        // Set CanSave to false so user will have to Submit again before Saving.
        CanSave = false;
        SavedConfigurationData = ConfigurationData;
        SetConfigure();
    }

    // Increase precision of SetDefaultDeveloperLoginId by matching the selectedQueryUrl's org
    // with the first matching DeveloperId that contains that org.
    protected override void SetDefaultDeveloperLoginId()
    {
        base.SetDefaultDeveloperLoginId();

        var azureOrg = new AzureUri(_selectedQueryUrl).Organization;
        if (!string.IsNullOrEmpty(azureOrg))
        {
            var devIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIds().DeveloperIds;
            if (devIds is null)
            {
                return;
            }

            DeveloperLoginId = devIds.Where(i => i.LoginId.Contains(azureOrg, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()?.LoginId ?? DeveloperLoginId;
        }
    }

    // Data loading methods
    public override void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Requestor.ToString() == Id)
        {
            Log.Debug($"Received matching query update");

            if (e.Kind == DataManagerUpdateKind.Error)
            {
                DataState = WidgetDataState.FailedUpdate;
                DataErrorMessage = e.Context.ErrorMessage;

                // The DataManager log will have detailed exception info, use the short message.
                Log.Error($"Data update failed. {e.Context.QueryId} {e.Context.ErrorMessage}");
                return;
            }

            DataState = WidgetDataState.Okay;
            DataErrorMessage = string.Empty;
            LoadContentData();
        }

        // If we failed data state, any data update might be an opportunity to retry since any
        // update means a transaction has completed.
        if (DataState == WidgetDataState.FailedRead)
        {
            Log.Debug("Retrying datastore read.");
            LoadContentData();
            return;
        }
    }

    public override void RequestContentData()
    {
        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            // Should not happen
            Log.Error("Failed to get DeveloperId");
            return;
        }

        var requestOptions = RequestOptions.RequestOptionsDefault();

        try
        {
            var azureUri = new AzureUri(_selectedQueryUrl);
            DataManager!.UpdateDataForQueryAsync(azureUri, developerId.LoginId, requestOptions, new Guid(Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed requesting data update.");
        }
    }

    protected override void ResetDataFromState(string data)
    {
        var dataObject = JsonObject.Parse(data);

        if (dataObject == null)
        {
            return;
        }

        _widgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
        DeveloperLoginId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
        _selectedQueryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
        _message = null;

        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            return;
        }

        var azureUri = new AzureUri(_selectedQueryUrl);
        _selectedQueryId = azureUri.Query;
        if (!azureUri.IsQuery)
        {
            Log.Error("Selected Query Url from ResetDataFromState is not a valid query.");
        }
    }

    public override string GetConfiguration(string data)
    {
        var configurationData = new JsonObject();

        var developerIdsData = new JsonArray();

        var devIdProvider = DeveloperIdProvider.GetInstance();

        foreach (var developerId in devIdProvider.GetLoggedInDeveloperIds().DeveloperIds)
        {
            developerIdsData.Add(new JsonObject
            {
                { "devId", developerId.LoginId },
            });
        }

        configurationData.Add("accounts", developerIdsData);

        configurationData.Add("selectedDevId", DeveloperLoginId);
        configurationData.Add("url", _selectedQueryUrl);
        configurationData.Add("message", _message);
        configurationData.Add("widgetTitle", _widgetTitle);

        configurationData.Add("pinned", Pinned);
        configurationData.Add("arrow", IconLoader.GetIconAsBase64("arrow.png"));

        return configurationData.ToString();
    }

    public override void LoadContentData()
    {
        try
        {
            var developerId = GetDevId(DeveloperLoginId);
            if (developerId == null)
            {
                // Should not happen, but may be possible in situations where the app is removed and
                // the signed in account is not silently restorable.
                // This is also checked before on UpdateActivityState() method on base class.
                Log.Error("Failed to get Dev ID");
                return;
            }

            var azureUri = new AzureUri(_selectedQueryUrl);

            if (!azureUri.IsQuery)
            {
                // This should never happen. Already was validated on configuration.
                Log.Error($"Invalid Uri: {_selectedQueryUrl}");
                return;
            }

            // This can throw if DataStore is not connected.
            var queryInfo = DataManager!.GetQuery(azureUri, developerId.LoginId);

            var queryResults = queryInfo is null ? new Dictionary<string, object>() : JsonConvert.DeserializeObject<Dictionary<string, object>>(queryInfo.QueryResults);

            var itemsData = new JsonObject();
            var itemsArray = new JsonArray();

            foreach (var element in queryResults!)
            {
                var workItem = JsonObject.Parse(element.Value.ToStringInvariant());

                if (workItem != null)
                {
                    // If we can't get the real date, it is better better to show a recent
                    // closer-to-correct time than the zero value decades ago, so use DateTime.UtcNow.
                    var dateTicks = workItem["System.ChangedDate"]?.GetValue<long>() ?? DateTime.UtcNow.Ticks;
                    var dateTime = dateTicks.ToDateTime();
                    var creator = DataManager.GetIdentity(workItem["System.CreatedBy"]?.GetValue<long>() ?? 0L);
                    var workItemType = DataManager.GetWorkItemType(workItem["System.WorkItemType"]?.GetValue<long>() ?? 0L);
                    var item = new JsonObject
                    {
                        { "title", workItem["System.Title"]?.GetValue<string>() ?? string.Empty },
                        { "url", workItem[AzureDataManager.WorkItemHtmlUrlFieldName]?.GetValue<string>() ?? string.Empty },
                        { "icon", GetIconForType(workItemType.Name) },
                        { "status_icon", GetIconForStatusState(workItem["System.State"]?.GetValue<string>()) },
                        { "number", element.Key },
                        { "date", TimeSpanHelper.DateTimeOffsetToDisplayString(dateTime, Log) },
                        { "user", creator.Name },
                        { "status", workItem["System.State"]?.GetValue<string>() ?? string.Empty },
                        { "avatar", creator.Avatar },
                    };

                    itemsArray.Add(item);
                }
            }

            itemsData.Add("workItemCount", queryInfo is null ? 0 : (int)queryInfo.QueryResultCount);
            itemsData.Add("maxItemsDisplayed", AzureDataManager.QueryResultLimit);
            itemsData.Add("items", itemsArray);
            itemsData.Add("widgetTitle", _widgetTitle);
            itemsData.Add("is_loading_data", DataState == WidgetDataState.Unknown);

            ContentData = itemsData.ToJsonString();
            UpdateActivityState();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error retrieving data.");
            DataState = WidgetDataState.FailedRead;
            return;
        }
    }

    // Overriding methods from Widget base
    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => @"Widgets\Templates\AzureSignInTemplate.json",
            WidgetPageState.Configure => @"Widgets\Templates\AzureQueryListConfigurationTemplate.json",
            WidgetPageState.Content => @"Widgets\Templates\AzureQueryListTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\AzureLoadingTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public override string GetData(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => GetSignIn(),
            WidgetPageState.Configure => GetConfiguration(string.Empty),
            WidgetPageState.Content => ContentData,
            WidgetPageState.Loading => EmptyJson,
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }
}
