// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.Widgets.Providers;
using Newtonsoft.Json;

namespace DevHomeAzureExtension.Widgets;

internal class AzurePullRequestsWidget : AzureWidget
{
    private readonly string sampleIconData = IconLoader.GetIconAsBase64("screenshot.png");

    protected static readonly new string Name = nameof(AzurePullRequestsWidget);

    private static readonly string DefaultSelectedView = "Mine";

    // Widget Data
    private string widgetTitle = string.Empty;
    private string selectedDevId = string.Empty;
    private string selectedRepositoryUrl = string.Empty;
    private string selectedRepositoryName = string.Empty;
    private string selectedView = DefaultSelectedView;
    private string? message;

    // Creation and destruction methods
    public AzurePullRequestsWidget()
    : base()
    {
    }

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        if (state.Any())
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
            Log.Logger()?.ReportError(Name, ShortId, $"Unknown Pull Request view for string: {viewStr}");
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

    // Action handler methods
    protected override void HandleSubmit(WidgetActionInvokedArgs args)
    {
        // Set loading page while we fetch data from ADO.
        Page = WidgetPageState.Loading;
        UpdateWidget();
        CanPin = false;

        // This is the action when the user clicks the submit button after entering some data on the widget while in
        // the Configure state.
        Page = WidgetPageState.Configure;
        var data = args.Data;
        var dataObject = JsonObject.Parse(data);
        message = null;
        if (dataObject != null && dataObject["account"] != null && dataObject["query"] != null)
        {
            widgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
            selectedDevId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
            selectedRepositoryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
            selectedView = dataObject["view"]?.GetValue<string>() ?? string.Empty;
            SetDefaultDeveloperId();
            if (selectedDevId != dataObject["account"]?.GetValue<string>())
            {
                dataObject["account"] = selectedDevId;
                data = dataObject.ToJsonString();
            }

            ConfigurationData = data;

            var developerId = GetDevId(selectedDevId);
            if (developerId == null)
            {
                message = Resources.GetResource(@"Widget_Template/DevIDError");
                UpdateActivityState();
                return;
            }

            var repositoryInfo = AzureClientHelpers.GetRepositoryInfo(selectedRepositoryUrl, developerId);
            if (repositoryInfo.Result != ResultType.Success)
            {
                message = GetMessageForError(repositoryInfo.Error, repositoryInfo.ErrorMessage);
                UpdateActivityState();
                return;
            }

            CanPin = true;
            message = Resources.GetResource(@"Widget_Template/CanBePinned");
        }

        Page = WidgetPageState.Configure;
        UpdateWidget();
    }

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        // Set CanPin to false so user will have to Submit again before Saving.
        CanPin = false;
        SavedConfigurationData = ConfigurationData;
        SetConfigure();
    }

    // This method will attempt to select a DeveloperId if one is not already selected.
    // It uses the input url and tries to find the most likely DeveloperId that corresponds to the
    // url among the set of available DeveloperIds. If there is no best match it chooses the first
    // available developerId or none if there are no DeveloperIds.
    private void SetDefaultDeveloperId()
    {
        if (!string.IsNullOrEmpty(selectedDevId))
        {
            return;
        }

        var devIds = DeveloperIdProvider.GetInstance().GetLoggedInDeveloperIds().DeveloperIds;
        if (devIds is null)
        {
            return;
        }

        // Set as the first DevId found, unless we find a better match from the Url.
        selectedDevId = devIds.FirstOrDefault()?.LoginId ?? string.Empty;
        var azureOrg = new AzureUri(selectedRepositoryUrl).Organization;
        if (!string.IsNullOrEmpty(azureOrg))
        {
            selectedDevId = devIds.Where(i => i.LoginId.Contains(azureOrg, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()?.LoginId ?? selectedDevId;
        }
    }

    // Data loading methods
    public override void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Requestor.ToString() == Id)
        {
            Log.Logger()?.ReportDebug(Name, ShortId, $"Received matching query update");

            if (e.Kind == DataManagerUpdateKind.Error)
            {
                DataState = WidgetDataState.Failed;
                DataErrorMessage = e.Context.ErrorMessage;

                // The DataManager log will have detailed exception info, use the short message.
                Log.Logger()?.ReportError(Name, ShortId, $"Data update failed. {e.Context.QueryId} {e.Context.ErrorMessage}");

                // TODO: Display error to user however design deems appropriate.
                // https://github.com/microsoft/DevHomeADOExtension/issues/50
                return;
            }

            DataState = WidgetDataState.Okay;
            DataErrorMessage = string.Empty;
            LoadContentData();
        }
    }

    public override void RequestContentData()
    {
        var developerId = GetDevId(selectedDevId);
        if (developerId == null)
        {
            // Should not happen
            Log.Logger()?.ReportError(Name, ShortId, "Failed to get DeveloperId");
            return;
        }

        try
        {
            var requestOptions = RequestOptions.RequestOptionsDefault();
            var azureUri = new AzureUri(selectedRepositoryUrl);
            DataManager!.UpdateDataForPullRequestsAsync(azureUri, developerId.LoginId, GetPullRequestView(selectedView), requestOptions, new Guid(Id));
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(Name, ShortId, "Failed requesting data update.", ex);
        }
    }

    private void ResetDataFromState(string data)
    {
        var dataObject = JsonObject.Parse(data);

        if (dataObject == null)
        {
            return;
        }

        widgetTitle = dataObject["widgetTitle"]?.GetValue<string>() ?? string.Empty;
        selectedDevId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
        selectedRepositoryUrl = dataObject["query"]?.GetValue<string>() ?? string.Empty;
        selectedView = dataObject["view"]?.GetValue<string>() ?? string.Empty;

        var developerId = GetDevId(selectedDevId);
        if (developerId == null)
        {
            return;
        }

        var azureUri = new AzureUri(selectedRepositoryUrl);
        selectedRepositoryName = azureUri.Repository;
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

        configurationData.Add("selectedDevId", selectedDevId);
        configurationData.Add("url", selectedRepositoryUrl);
        configurationData.Add("selectedView", selectedView);
        configurationData.Add("message", message);
        configurationData.Add("widgetTitle", widgetTitle);

        configurationData.Add("configuring", !CanPin);
        configurationData.Add("pinned", Pinned);
        configurationData.Add("arrow", IconLoader.GetIconAsBase64("arrow.png"));

        return configurationData.ToString();
    }

    public override void LoadContentData()
    {
        var developerId = GetDevId(selectedDevId);
        if (developerId == null)
        {
            // Should not happen
            Log.Logger()?.ReportError(Name, ShortId, "Failed to get Dev ID");
            return;
        }

        var azureUri = new AzureUri(selectedRepositoryUrl);
        if (!azureUri.IsRepository)
        {
            Log.Logger()?.ReportError(Name, ShortId, $"Invalid Uri: {selectedRepositoryUrl}");
            return;
        }

        PullRequests? pullRequests;
        try
        {
            // This can throw if DataStore is not connected.
            pullRequests = DataManager!.GetPullRequests(azureUri, developerId.LoginId, GetPullRequestView(selectedView));
            if (pullRequests == null)
            {
                // This is not always an error and this will happen for a new widget when it is first
                // pinned before we actually have data populated. Treating this as an information log
                // event rather than an error.
                Log.Logger()?.ReportInfo(Name, ShortId, $"No pull request information found for {azureUri.Organization}/{azureUri.Project}/{azureUri.Repository}");
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(Name, ShortId, $"GetPullRequests failed.", ex);
            return;
        }

        var pullRequestsResults = JsonConvert.DeserializeObject<Dictionary<string, object>>(pullRequests.Results);

        var itemsData = new JsonObject();
        var itemsArray = new JsonArray();

        foreach (var element in pullRequestsResults)
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
                    { "date", TimeSpanHelper.DateTimeOffsetToDisplayString(dateTime, Log.Logger()) },
                    { "user", workItem["CreatedBy"]?["Name"]?.GetValue<string>() ?? string.Empty },
                    { "branch", workItem["TargetBranch"]?.GetValue<string>().Replace("refs/heads/", string.Empty) },
                    { "avatar", workItem["CreatedBy"]?["Avatar"]?.GetValue<string>() },
                };

                itemsArray.Add(item);
            }
        }

        itemsData.Add("items", itemsArray);
        itemsData.Add("widgetTitle", widgetTitle);

        ContentData = itemsData.ToJsonString();
        UpdateActivityState();
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
            WidgetPageState.Loading => new JsonObject { { "configuring", true } }.ToJsonString(),
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }
}
