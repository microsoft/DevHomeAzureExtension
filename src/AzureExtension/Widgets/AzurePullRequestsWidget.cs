﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.Widgets.Providers;
using Newtonsoft.Json;

namespace DevHomeAzureExtension.Widgets;

internal sealed class AzurePullRequestsWidget : AzureWidget
{
    private readonly string _sampleIconData = IconLoader.GetIconAsBase64("screenshot.png");

    private const string DefaultSelectedView = "Mine";

    // Widget Data
    private string _widgetTitle = string.Empty;
    private string _selectedRepositoryUrl = string.Empty;
    private string _selectedRepositoryName = string.Empty;
    private string _selectedView = DefaultSelectedView;
    private string? _message;

    // Creation and destruction methods
    public AzurePullRequestsWidget()
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
    private PullRequestView GetPullRequestView(string viewStr)
    {
        try
        {
            return Enum.Parse<PullRequestView>(viewStr);
        }
        catch (Exception)
        {
            Log.Error($"Unknown Pull Request view for string: {viewStr}");
            return PullRequestView.Unknown;
        }
    }

    private string GetIconForPullRequestStatus(string? prStatus)
    {
        return prStatus switch
        {
            "Approved" => IconLoader.GetIconAsBase64("PullRequestApproved.png"),
            "Waiting" => IconLoader.GetIconAsBase64("PullRequestWaiting.png"),
            "Rejected" => IconLoader.GetIconAsBase64("PullRequestRejected.png"),
            _ => IconLoader.GetIconAsBase64("PullRequestReviewNotStarted.png"),
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
            _widgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
            DeveloperLoginId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
            _selectedRepositoryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
            _selectedView = dataObject["view"]?.GetValue<string>() ?? string.Empty;
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

            var repositoryInfo = AzureClientHelpers.GetRepositoryInfo(_selectedRepositoryUrl, developerId);
            if (repositoryInfo.Result != ResultType.Success)
            {
                _message = GetMessageForError(repositoryInfo.Error, repositoryInfo.ErrorMessage);
                UpdateActivityState();
                return false;
            }

            CanSave = true;
            Pinned = true;
            Page = WidgetPageState.Content;
            UpdateActivityState();
            return true;
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

    // Increase precision of SetDefaultDeveloperLoginId by matching the selectedRepositoryUrl's org
    // with the first matching DeveloperId that contains that org.
    protected override void SetDefaultDeveloperLoginId()
    {
        base.SetDefaultDeveloperLoginId();
        var azureOrg = new AzureUri(_selectedRepositoryUrl).Organization;
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
                DataState = WidgetDataState.Failed;
                DataErrorMessage = e.Context.ErrorMessage;

                // The DataManager log will have detailed exception info, use the short message.
                Log.Error($"Data update failed. {e.Context.QueryId} {e.Context.ErrorMessage}");
                return;
            }

            DataState = WidgetDataState.Okay;
            DataErrorMessage = string.Empty;
            LoadContentData();
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

        try
        {
            var requestOptions = RequestOptions.RequestOptionsDefault();
            var azureUri = new AzureUri(_selectedRepositoryUrl);
            DataManager!.UpdateDataForPullRequestsAsync(azureUri, developerId.LoginId, GetPullRequestView(_selectedView), requestOptions, new Guid(Id));
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
        _selectedRepositoryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
        _selectedView = dataObject["view"]?.GetValue<string>() ?? string.Empty;
        _message = null;

        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            return;
        }

        var azureUri = new AzureUri(_selectedRepositoryUrl);
        _selectedRepositoryName = azureUri.Repository;
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
        configurationData.Add("url", _selectedRepositoryUrl);
        configurationData.Add("selectedView", _selectedView);
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
                // Should not happen
                Log.Error("Failed to get Dev ID");
                return;
            }

            var azureUri = new AzureUri(_selectedRepositoryUrl);
            if (!azureUri.IsRepository)
            {
                Log.Error($"Invalid Uri: {_selectedRepositoryUrl}");
                return;
            }

            PullRequests? pullRequests;

            // This can throw if DataStore is not connected.
            pullRequests = DataManager!.GetPullRequests(azureUri, developerId.LoginId, GetPullRequestView(_selectedView));

            var pullRequestsResults = pullRequests is null ? new Dictionary<string, object>() : JsonConvert.DeserializeObject<Dictionary<string, object>>(pullRequests.Results);

            var itemsData = new JsonObject();
            var itemsArray = new JsonArray();

            foreach (var element in pullRequestsResults!)
            {
                var workItem = JsonObject.Parse(element.Value.ToStringInvariant());

                if (workItem != null)
                {
                    // If we can't get the real date, it is better better to show a recent
                    // closer-to-correct time than the zero value decades ago, so use DateTime.Now.
                    var dateTicks = workItem["CreationDate"]?.GetValue<long>() ?? DateTime.Now.Ticks;
                    var dateTime = dateTicks.ToDateTime();

                    var item = new JsonObject
                    {
                        { "title", workItem["Title"]?.GetValue<string>() ?? string.Empty },
                        { "url", workItem["HtmlUrl"]?.GetValue<string>() ?? string.Empty },
                        { "status_icon", GetIconForPullRequestStatus(workItem["Status"]?.GetValue<string>()) },
                        { "number", element.Key },
                        { "date", TimeSpanHelper.DateTimeOffsetToDisplayString(dateTime, Log) },
                        { "user", workItem["CreatedBy"]?["Name"]?.GetValue<string>() ?? string.Empty },
                        { "branch", workItem["TargetBranch"]?.GetValue<string>().Replace("refs/heads/", string.Empty) },
                        { "avatar", workItem["CreatedBy"]?["Avatar"]?.GetValue<string>() },
                    };

                    itemsArray.Add(item);
                }
            }

            itemsData.Add("maxItemsDisplayed", AzureDataManager.PullRequestResultLimit);
            itemsData.Add("items", itemsArray);
            itemsData.Add("widgetTitle", _widgetTitle);
            itemsData.Add("is_loading_data", DataState == WidgetDataState.Unknown);

            ContentData = itemsData.ToJsonString();
            UpdateActivityState();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error retrieving data.");
            DataState = WidgetDataState.Failed;
            return;
        }
    }

    // Overriding methods from Widget base
    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => @"Widgets\Templates\AzureSignInTemplate.json",
            WidgetPageState.Configure => @"Widgets\Templates\AzurePullRequestsConfigurationTemplate.json",
            WidgetPageState.Content => @"Widgets\Templates\AzurePullRequestsTemplate.json",
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
