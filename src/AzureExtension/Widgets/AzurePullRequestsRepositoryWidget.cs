// Copyright (c) Microsoft Corporation.
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

internal sealed class AzurePullRequestsRepositoryWidget : AzurePullRequestsBaseWidget
{
    private static readonly string _defaultSelectedView = PullRequestView.Mine.ToString();

    // Widget Data
    private string _selectedRepositoryUrl = string.Empty;
    private string _selectedView = _defaultSelectedView;

    // Creation and destruction methods
    public AzurePullRequestsRepositoryWidget()
    : base()
    {
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

    // Data loading methods
    public override void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Requestor.ToString() == Id)
        {
            Log.Debug($"Received matching DataStore update");

            if (e.Kind == DataManagerUpdateKind.Error)
            {
                DataState = WidgetDataState.FailedUpdate;
                DataErrorMessage = e.Context.ErrorMessage;

                // The DataManager log will have detailed exception info, use the short message.
                Log.Error($"Data update failed. {e.Context.ErrorMessage}");

                if (!LoadedDataSuccessfully)
                {
                    // We have not loaded data successfully, so show a loading page
                    // instead of an error page.
                    SetLoading();
                }

                return;
            }

            DataState = WidgetDataState.Okay;
            DataErrorMessage = string.Empty;
            LoadContentData();
            return;
        }

        // If we failed data state, any data update might be an opportunity to retry since any
        // update means a transaction has completed.
        if (DataState == WidgetDataState.FailedRead)
        {
            Log.Debug("Retrying datastore read.");
            LoadContentData();
            return;
        }

        // If we failed to update, try again after a data update has been received.
        if (DataState == WidgetDataState.FailedUpdate)
        {
            Log.Debug("Retrying datastore update.");
            RequestContentData();
            return;
        }
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
        Message = null;
        if (dataObject != null && dataObject["account"] != null && dataObject["query"] != null)
        {
            WidgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
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
                Message = Resources.GetResource(@"Widget_Template/DevIDError");
                UpdateActivityState();
                return false;
            }

            var repositoryInfo = AzureClientHelpers.GetRepositoryInfo(_selectedRepositoryUrl, developerId);
            if (repositoryInfo.Result != ResultType.Success)
            {
                Message = GetMessageForError(repositoryInfo.Error, repositoryInfo.ErrorMessage);
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

    // Increase precision of SetDefaultDeveloperLoginId by matching the selectedRepositoryUrl's org
    // with the first matching DeveloperId that contains that org.
    protected override void SetDefaultDeveloperLoginId()
    {
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

    public override void RequestContentData()
    {
        if (DataState == WidgetDataState.Requested)
        {
            return;
        }

        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            // Should not happen
            Log.Error("Failed to get DeveloperId");
            DataState = WidgetDataState.FailedUpdate;
            return;
        }

        try
        {
            var requestOptions = RequestOptions.RequestOptionsDefault();
            var azureUri = new AzureUri(_selectedRepositoryUrl);
            DataState = WidgetDataState.Requested;
            DataManager!.UpdateDataForPullRequestsAsync(azureUri, developerId.LoginId, GetPullRequestView(_selectedView), requestOptions, new Guid(Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed requesting data update.");
            DataState = WidgetDataState.FailedUpdate;
        }
    }

    protected override void ResetDataFromState(string data)
    {
        var dataObject = JsonObject.Parse(data);

        if (dataObject == null)
        {
            return;
        }

        WidgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
        DeveloperLoginId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
        _selectedRepositoryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
        _selectedView = dataObject["view"]?.GetValue<string>() ?? string.Empty;
        Message = null;

        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            return;
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
        configurationData.Add("url", _selectedRepositoryUrl);
        configurationData.Add("selectedView", _selectedView);
        configurationData.Add("message", Message);
        configurationData.Add("widgetTitle", WidgetTitle);

        configurationData.Add("pinned", Pinned);
        configurationData.Add("arrow", IconLoader.GetIconAsBase64("arrow.png"));

        return configurationData.ToString();
    }

    public override void LoadContentData()
    {
        if (!LoadedDataSuccessfully)
        {
            SetLoading();
        }

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
                    // closer-to-correct time than the zero value decades ago, so use DateTime.UtcNow.
                    var dateTicks = workItem["CreationDate"]?.GetValue<long>() ?? DateTime.UtcNow.Ticks;
                    var dateTime = dateTicks.ToDateTime();
                    var creator = DataManager.GetIdentity(workItem["CreatedBy"]?.GetValue<long>() ?? 0L);
                    var item = new JsonObject
                    {
                        { "title", workItem["Title"]?.GetValue<string>() ?? string.Empty },
                        { "url", workItem["HtmlUrl"]?.GetValue<string>() ?? string.Empty },
                        { "status_icon", GetIconForPullRequestStatus(workItem["PolicyStatus"]?.GetValue<string>()) },
                        { "number", element.Key },
                        { "date", TimeSpanHelper.DateTimeOffsetToDisplayString(dateTime, Log) },
                        { "user", creator.Name },
                        { "branch", workItem["TargetBranch"]?.GetValue<string>().Replace("refs/heads/", string.Empty) },
                        { "avatar", creator.Avatar },
                    };

                    itemsArray.Add(item);
                }
            }

            if (string.IsNullOrEmpty(WidgetTitle))
            {
                WidgetTitle = azureUri.Repository;
            }

            itemsData.Add("maxItemsDisplayed", AzureDataManager.PullRequestResultLimit);
            itemsData.Add("items", itemsArray);
            itemsData.Add("widgetTitle", WidgetTitle);
            itemsData.Add("is_loading_data", DataState != WidgetDataState.Unknown);

            ContentData = itemsData.ToJsonString();
            DataState = WidgetDataState.Okay;
            LoadedDataSuccessfully = true;
            UpdateActivityState();
        }
        catch (Exception e)
        {
            Log.Error(e, "Error retrieving data.");
            DataState = WidgetDataState.FailedRead;
            if (!LoadedDataSuccessfully)
            {
                SetLoading();
            }

            return;
        }
    }
}
