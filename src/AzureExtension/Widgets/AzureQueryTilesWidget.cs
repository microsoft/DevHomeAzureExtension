// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Widgets.Enums;
using Microsoft.Windows.Widgets.Providers;

namespace DevHomeAzureExtension.Widgets;

internal class AzureQueryTilesWidget : AzureWidget
{
    private readonly string sampleIconData = IconLoader.GetIconAsBase64("screenshot.png");

    private readonly int maxNumLines = 3;
    private readonly int maxNumColumns = 2;

    // Widget data
    protected static readonly new string Name = nameof(AzureQueryTilesWidget);
    private readonly List<QueryTile> tiles = new();

    // Creation and destruction methods
    public AzureQueryTilesWidget()
    : base()
    {
    }

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        if (state.Any())
        {
            // Not newly created widget, recovering tiles from state
            ResetNumberOfTilesFromData(state);
            UpdateAllTiles(state);
            ValidateConfigurationData();
        }
        else
        {
            // Start with one initial tile.
            tiles.Add(new QueryTile(
              string.Empty,
              string.Empty));
        }

        base.CreateWidget(widgetContext, state);
    }

    public override void DeleteWidget(string widgetId, string customState)
    {
        base.DeleteWidget(widgetId, customState);
    }

    // Action handler methods
    protected void HandleAddTile(WidgetActionInvokedArgs args)
    {
        Page = WidgetPageState.Loading;
        UpdateWidget();

        UpdateAllTiles(args.Data);

        tiles.Add(new QueryTile(
              string.Empty,
              string.Empty));

        ValidateConfigurationData();

        Page = WidgetPageState.Configure;
        UpdateWidget();
    }

    private string RemoveLastTileFromData(string data)
    {
        var dataObject = JsonObject.Parse(data)!.AsObject();

        dataObject.Remove($"tileTitle{tiles.Count}");
        dataObject.Remove($"query{tiles.Count}");

        return dataObject.ToJsonString();
    }

    protected void HandleRemoveTile(WidgetActionInvokedArgs args)
    {
        Page = WidgetPageState.Loading;
        UpdateWidget();

        if (tiles.Any())
        {
            tiles.RemoveAt(tiles.Count - 1);
        }

        UpdateAllTiles(RemoveLastTileFromData(args.Data));
        ValidateConfigurationData();

        Page = WidgetPageState.Configure;
        UpdateWidget();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);
        Log.Logger()?.ReportDebug(Name, ShortId, $"ActionInvoked: {verb}");

        switch (verb)
        {
            case WidgetAction.AddTile:
                HandleAddTile(actionInvokedArgs);
                break;

            case WidgetAction.RemoveTile:
                HandleRemoveTile(actionInvokedArgs);
                break;

            case WidgetAction.SignIn:
                _ = HandleSignIn();
                break;

            case WidgetAction.Save:
                if (ValidateConfiguration(actionInvokedArgs))
                {
                    SavedConfigurationData = string.Empty;
                    ContentData = EmptyJson;
                    DataState = WidgetDataState.Unknown;
                    SetActive();
                }

                break;

            case WidgetAction.Cancel:
                // Put back the configuration data from before we started changing the widget.
                ConfigurationData = SavedConfigurationData;
                SavedConfigurationData = string.Empty;
                CanSave = true;

                // Tiles were updated on Save, restore them from the saved configuration data.
                ResetDataFromState(ConfigurationData);

                SetActive();
                break;

            case WidgetAction.Unknown:
                Log.Logger()?.ReportError(Name, ShortId, $"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }
    }

    protected override bool ValidateConfiguration(WidgetActionInvokedArgs actionInvokedArgs)
    {
        Page = WidgetPageState.Loading;
        UpdateWidget();

        UpdateAllTiles(actionInvokedArgs.Data);
        bool hasValidData = ValidateConfigurationData();
        if (hasValidData)
        {
            Page = WidgetPageState.Content;
            Pinned = true;
        }
        else
        {
            Page = WidgetPageState.Configure;
        }

        UpdateActivityState();
        return hasValidData;
    }

    public override void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        // Set CanSave to false so user will have to Submit again before Saving.
        CanSave = false;
        SavedConfigurationData = ConfigurationData;
        SetConfigure();
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
            Log.Logger()?.ReportError(Name, ShortId, "Failed to get DeveloperId");
            return;
        }

        var queryList = new List<AzureUri>();
        foreach (var tile in tiles)
        {
            queryList.Add(tile.AzureUri);
        }

        try
        {
            var requestOptions = RequestOptions.RequestOptionsDefault();
            DataManager!.UpdateDataForQueriesAsync(queryList, developerId.LoginId, requestOptions, new Guid(Id));
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(Name, ShortId, "Failed requesting data update.", ex);
        }

        Log.Logger()?.ReportInfo(Name, ShortId, $"Requested data update for this widget.");
        DataState = WidgetDataState.Requested;
    }

    protected override void ResetDataFromState(string state)
    {
        ResetNumberOfTilesFromData(ConfigurationData);
        UpdateAllTiles(ConfigurationData);
        ValidateConfigurationData();
    }

    public override void LoadContentData()
    {
        var data = new JsonObject();
        var linesArray = new JsonArray();

        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            // Should not happen
            Log.Logger()?.ReportError(Name, ShortId, "Failed to get Dev ID");
            return;
        }

        for (var i = 0; i < maxNumLines; ++i)
        {
            var tilesArray = new JsonArray();
            for (var j = 0; j < maxNumColumns; ++j)
            {
                var pos = (2 * i) + j;

                if (pos < tiles.Count)
                {
                    var workItemCount = 0;
                    try
                    {
                        var queryInfo = DataManager!.GetQuery(tiles[pos].AzureUri.Query, developerId.LoginId);
                        if (queryInfo == null)
                        {
                            // Failing this query doesn't mean the other queries won't fail.
                            Log.Logger()?.ReportInfo(Name, ShortId, $"No query information found for query: {tiles[pos].AzureUri.Query}");
                        }
                        else
                        {
                            workItemCount = (int)queryInfo.QueryResultCount;
                        }

                        var tile = new JsonObject
                        {
                            { "title", tiles[pos].Title },
                            { "counter", workItemCount },
                            { "backgroundImage", IconLoader.GetIconAsBase64("BlueBackground.png") },
                            { "url", tiles[pos].AzureUri.ToString() },
                        };

                        tilesArray.Add(tile);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger()?.ReportError(Name, ShortId, $"Failed getting query info or creating the tile object.", ex);
                        return;
                    }
                }
            }

            linesArray.Add(new JsonObject { { "tiles", tilesArray } });
        }

        data.Add("lines", linesArray);

        Log.Logger()?.ReportDebug(Name, ShortId, data.ToJsonString());

        ContentData = data.ToJsonString();

        UpdateActivityState();
    }

    private bool ValidateConfigurationData()
    {
        CanSave = false;
        var pinIssueFound = false;

        SetDefaultDeveloperLoginId();
        var developerId = GetDevId(DeveloperLoginId);
        if (developerId == null)
        {
            return false;
        }

        if (!tiles.Any())
        {
            return false;
        }

        for (var i = 0; i < tiles.Count; ++i)
        {
            var tile = tiles[i];
            if (tile.Validated)
            {
                // Skip tiles that have already been validated.
                continue;
            }

            var queryInfo = AzureClientHelpers.GetQueryInfo(tile.AzureUri, developerId);
            if (queryInfo.Result == ResultType.Success)
            {
                tile.Validated = true;
                tile.Message = string.Empty;
            }
            else
            {
                tile.Message = GetMessageForError(queryInfo.Error, queryInfo.ErrorMessage);
                pinIssueFound = true;
            }

            if (string.IsNullOrEmpty(tile.Title))
            {
                tile.Title = queryInfo.Name;
            }

            tiles[i] = tile;
        }

        CanSave = !pinIssueFound;

        return !pinIssueFound;
    }

    private void ResetNumberOfTilesFromData(string data)
    {
        var dataObject = JsonObject.Parse(data);

        if (dataObject == null)
        {
            return;
        }

        tiles.Clear();

        for (var i = 0; i < 6; ++i)
        {
            if (dataObject[$"query{i}"] != null && dataObject[$"tileTitle{i}"] != null)
            {
                tiles.Add(new QueryTile());
            }
        }
    }

    // Increase precision of SetDefaultDeveloperLoginId by matching the first valid org in the
    // tiles list with the first matching DeveloperId that contains that org.
    protected override void SetDefaultDeveloperLoginId()
    {
        base.SetDefaultDeveloperLoginId();
        var azureOrg = tiles.Where(i => i.AzureUri.IsValid).FirstOrDefault().AzureUri?.Organization ?? string.Empty;
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

    private void UpdateAllTiles(string data)
    {
        var dataObject = JsonObject.Parse(data);
        var nTiles = tiles.Count;

        if (dataObject != null)
        {
            for (var i = 0; i < nTiles; ++i)
            {
                var tile = tiles[i];
                var queryFromData = dataObject[$"query{i}"]?.GetValue<string>() ?? string.Empty;
                var titleFromData = dataObject[$"tileTitle{i}"]?.GetValue<string>() ?? string.Empty;

                if (tile.AzureUri.OriginalString == queryFromData)
                {
                    // Uri was not changed by the user since the last update, so update the title
                    // if it is different, otherwise leave it alone so we only validate once.
                    if (tile.Title != titleFromData)
                    {
                        tile.Title = titleFromData;
                        tiles[i] = tile;
                    }
                }
                else
                {
                    // The Uri was changed in the ux, we need to recreate the tile with the new Uri.
                    tiles[i] = new QueryTile(queryFromData, titleFromData);
                }
            }

            DeveloperLoginId = dataObject["account"]?.GetValue<string>() ?? string.Empty;
            SetDefaultDeveloperLoginId();
            if (DeveloperLoginId != dataObject["account"]?.GetValue<string>())
            {
                dataObject["account"] = DeveloperLoginId;
                data = dataObject.ToJsonString();
            }

            ConfigurationData = data;
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

        var tilesArray = new JsonArray();

        for (var i = 0; i < tiles.Count; ++i)
        {
            tilesArray.Add(new JsonObject
            {
                { "url", tiles[i].AzureUri.ToString() },
                { "title", tiles[i].Title },
                { "message", tiles[i].Message.Any() ? tiles[i].Message : null },
            });
        }

        configurationData.Add("tiles", tilesArray);
        configurationData.Add("selectedDevId", DeveloperLoginId);
        configurationData.Add("pinned", Pinned);
        configurationData.Add("arrow", IconLoader.GetIconAsBase64("arrow.png"));

        return configurationData.ToString();
    }

    // Overriding methods from Widget base
    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => @"Widgets\Templates\AzureSignInTemplate.json",
            WidgetPageState.Configure => @"Widgets\Templates\AzureQueryTilesConfigurationTemplate.json",
            WidgetPageState.Content => @"Widgets\Templates\AzureQueryTilesTemplate.json",
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

    internal struct QueryTile
    {
        public string Title = string.Empty;
        public string Message = string.Empty;
        public bool Validated = false;

        public readonly AzureUri AzureUri { get; }

        public QueryTile()
            : this(string.Empty, string.Empty)
        {
        }

        public QueryTile(string url, string title)
        {
            AzureUri = new AzureUri(url);
            Title = title;
        }
    }
}
