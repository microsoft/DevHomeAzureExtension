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

internal sealed class AzurePullRequestsDeveloperWidget : AzurePullRequestsBaseWidget
{
    private readonly CacheManager _cacheManager;

    public AzurePullRequestsDeveloperWidget()
    : base()
    {
        SupportsCustomization = false;
        DeveloperIdLoginRequired = false;
        _cacheManager = CacheManager.GetInstance();
        _cacheManager.OnUpdate += HandleCacheManagerUpdate;
    }

    ~AzurePullRequestsDeveloperWidget()
    {
        try
        {
            _cacheManager.OnUpdate -= HandleCacheManagerUpdate;
        }
        catch
        {
            // Best effort.
        }
    }

    private void HandleCacheManagerUpdate(object? source, CacheManagerUpdateEventArgs e)
    {
        if (e.Kind == CacheManagerUpdateKind.Updated)
        {
            ////
        }
    }

    // Data loading methods
    public override void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Kind == DataManagerUpdateKind.Developer)
        {
            Log.Debug($"Received developer pull request update.");

            if (e.Kind == DataManagerUpdateKind.Error)
            {
                DataState = WidgetDataState.FailedUpdate;
                DataErrorMessage = e.Context.ErrorMessage;

                // The DataManager log will have detailed exception info, use the short message.
                Log.Error($"Developer pull request update failed. {e.Context.ErrorMessage}");

                if (!LoadedDataSuccessfully)
                {
                    SetLoading();
                }

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

    protected override bool ValidateConfiguration(WidgetActionInvokedArgs args)
    {
        return true;
    }

    public override void RequestContentData()
    {
        // This widget does not request data, so we will use the periodic update
        // as a refresh to try to recover from any transient issue where
        // data was unavailable for whatever reason.
        LoadContentData();
    }

    protected override void ResetDataFromState(string data)
    {
        // This widget has no state.
        return;
    }

    public override string GetConfiguration(string data)
    {
        // This widget has no configuration.
        return string.Empty;
    }

    public override void LoadContentData()
    {
        if (!LoadedDataSuccessfully)
        {
            SetLoading();
        }

        try
        {
            // This can throw if DataStore is not connected.
            var pullRequestsList = DataManager!.GetPullRequestsForLoggedInDeveloperIds();

            if (!pullRequestsList.Any() && _cacheManager.UpdateInProgress)
            {
                // First time load might not find any pull requests becuase we haven't updated the
                // repository cache yet.
                Log.Debug("Cache is being updated, displaying loading page.");
                SetLoading();
                return;
            }

            var itemsData = new JsonObject();
            var itemsArray = new JsonArray();

            foreach (var pullRequests in pullRequestsList)
            {
                var pullRequestsResults = pullRequests is null ? [] : JsonConvert.DeserializeObject<Dictionary<string, object>>(pullRequests.Results);
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
                            { "dateTicks", dateTicks },
                            { "user", creator.Name },
                            { "branch", workItem["TargetBranch"]?.GetValue<string>().Replace("refs/heads/", string.Empty) },
                            { "avatar", creator.Avatar },
                        };

                        itemsArray.Add(item);
                    }
                }
            }

            try
            {
                // Sort all pull requests by creation date, descending so newer are at the top of the list.
                var sortedItems = itemsArray.OrderByDescending(x => x?["dateTicks"]?.GetValue<long>());
            }
            catch (Exception innerJsonEx)
            {
                Log.Error(innerJsonEx, $"Json sort failed {innerJsonEx.Message}");
            }

            itemsData.Add("maxItemsDisplayed", AzureDataManager.PullRequestResultLimit);
            itemsData.Add("items", itemsArray);
            itemsData.Add("widgetTitle", WidgetTitle);
            itemsData.Add("is_loading_data", DataState == WidgetDataState.Unknown);

            ContentData = itemsData.ToJsonString();
            DataState = WidgetDataState.Okay;
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

    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => @"Widgets\Templates\AzureSignInTemplate.json",
            WidgetPageState.Content => @"Widgets\Templates\AzurePullRequestsTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\AzureLoadingTemplate.json",
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }

    public override string GetData(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => GetSignIn(),
            WidgetPageState.Content => ContentData,
            WidgetPageState.Loading => EmptyJson,
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }
}
