// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Storage;
using TFModels = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace DevHomeAzureExtension;

public partial class AzureDataManager : IAzureDataManager, IDisposable
{
    public static event DataManagerUpdateEventHandler? OnUpdate;

    public static readonly string RecreateDataStoreSettingsKey = "RecreateDataStore";

    public static readonly string IdentityRefFieldValueName = "Microsoft.VisualStudio.Services.WebApi.IdentityRef";

    public static readonly string SystemIdFieldName = "System.Id";

    public static readonly string WorkItemHtmlUrlFieldName = "DevHome.AzureExtension.WorkItemHtmlUrl";

    public static readonly string WorkItemTypeFieldName = "System.WorkItemType";

    // Max number pull requests to fetch for a repository search.
    public static readonly int PullRequestResultLimit = 25;

    // Max number of query results to fetch for a given query.
    public static readonly int QueryResultLimit = 26;

    // Most data that has not been updated within this time will be removed.
    private static readonly TimeSpan DataRetentionTime = TimeSpan.FromDays(1);

    private static readonly string LastUpdatedKeyName = "LastUpdated";

    private static readonly string Name = nameof(AzureDataManager);

    private static readonly ConcurrentDictionary<string, Guid> Instances = new();

    // Connections are a pairing of DeveloperId and a Uri.
    private static readonly ConcurrentDictionary<Tuple<Uri, DeveloperId.DeveloperId>, VssConnection> Connections = new();

    private readonly ILogger _log;

    private Guid UniqueName { get; } = Guid.NewGuid();

    private string InstanceName { get; } = string.Empty;

    private DataStore DataStore { get; set; }

    public DataStoreOptions DataStoreOptions { get; private set; }

    public static IAzureDataManager? CreateInstance(string identifier, DataStoreOptions? options = null)
    {
        options ??= DefaultOptions;

        try
        {
            return new AzureDataManager(identifier, options);
        }
        catch (Exception e)
        {
            var log = Log.ForContext("SourceContext", Name);
            log.Error(e, $"Failed creating AzureDataManager for {identifier}");
            return null;
        }
    }

    public AzureDataManager(string identifier, DataStoreOptions dataStoreOptions)
    {
        if (dataStoreOptions.DataStoreSchema == null)
        {
            throw new ArgumentNullException(nameof(dataStoreOptions), "DataStoreSchema cannot be null.");
        }

        InstanceName = identifier;
        _log = Log.ForContext("SourceContext", $"{Name}/{InstanceName}");
        DataStoreOptions = dataStoreOptions;
        DataStore = new DataStore(
            "DataStore",
            Path.Combine(dataStoreOptions.DataStoreFolderPath, dataStoreOptions.DataStoreFileName),
            dataStoreOptions.DataStoreSchema);
        DataStore.Create(dataStoreOptions.RecreateDataStore);

        try
        {
            // DeveloperIdProvider will attempt to do a lot of things if this is the first instance
            // of it being created. If this provider fails or the change handler is not successfully
            // created, that is not enough to warrant failing the Data Manager creation. Worst-case
            // we will not function with DeveloperId change events, which are not critical for
            // the data manager to do its job.
            DeveloperIdProvider.GetInstance().Changed += HandleDeveloperIdChange;
        }
        catch (Exception ex)
        {
            // This will likely fail during tests since DeveloperIdProvider uses ApplicationData.
            _log.Warning(ex, "Failed setting DeveloperId change handler.");
        }

        if (Instances.TryGetValue(InstanceName, out var instanceIdentifier))
        {
            // We should not have duplicate AzureDataManagers, as every client should have one,
            // but the identifiers may not be unique if using partial Guids. Note in the log
            // the duplicate as a warning and the existing unique name so we can see in the log what
            // client created the duplicate in order to discern random chance / consistent pattern.
            _log.Warning($"Duplicate instance created for identifier {InstanceName}:{instanceIdentifier}.");
        }
        else
        {
            Instances.TryAdd(InstanceName, UniqueName);
        }

        _log.Information($"Created AzureDataManager: {UniqueName}.");
    }

    ~AzureDataManager()
    {
        try
        {
            DeveloperIdProvider.GetInstance().Changed -= HandleDeveloperIdChange;
        }
        catch
        {
            // Best effort and we shouldn't have anything throwing during a destructor.
        }
    }

    public DateTime LastUpdated
    {
        get
        {
            ValidateDataStore();
            var lastUpdated = MetaData.Get(DataStore, LastUpdatedKeyName);
            if (lastUpdated == null)
            {
                return DateTime.MinValue;
            }

            return lastUpdated.ToDateTime();
        }
    }

    public override string ToString() => $"{Name}/{InstanceName}";

    public async Task UpdateDataForQueriesAsync(IEnumerable<AzureUri> queryUris, string developerLogin, RequestOptions? options = null, Guid? requestor = null)
    {
        ValidateDataStore();
        var parameters = new DataStoreOperationParameters
        {
            Uris = queryUris,
            LoginId = developerLogin,
            DeveloperId = DeveloperIdProvider.GetInstance().GetDeveloperIdFromAccountIdentifier(developerLogin),
            RequestOptions = options ?? RequestOptions.RequestOptionsDefault(),
            OperationName = "UpdateDataForQueryAsync",
            Requestor = requestor ?? Guid.NewGuid(),
        };

        dynamic context = new ExpandoObject();
        var contextDict = (IDictionary<string, object>)context;
        contextDict.Add("Queries", string.Join(",", queryUris));
        contextDict.Add("DeveloperLogin", developerLogin);
        contextDict.Add("Requestor", parameters.Requestor);

        try
        {
            await UpdateDataStoreAsync(parameters, UpdateDataForQueriesAsync);
        }
        catch (Exception ex)
        {
            contextDict.Add("ErrorMessage", ex.Message);
            SendErrorUpdateEvent(_log, this, parameters.Requestor, context, ex);
            return;
        }

        SendQueryUpdateEvent(_log, this, parameters.Requestor, context);
    }

    public async Task UpdateDataForQueryAsync(AzureUri queryUri, string developerLogin, RequestOptions? options = null, Guid? requestor = null)
    {
        var queryUris = new List<AzureUri> { queryUri };
        await UpdateDataForQueriesAsync(queryUris, developerLogin, options, requestor);
    }

    public async Task UpdateDataForPullRequestsAsync(AzureUri repositoryUri, string developerLogin, PullRequestView view, RequestOptions? options = null, Guid? requestor = null)
    {
        ValidateDataStore();
        var parameters = new DataStoreOperationParameters
        {
            Uris = new List<AzureUri> { repositoryUri },
            LoginId = developerLogin,
            DeveloperId = DeveloperIdProvider.GetInstance().GetDeveloperIdFromAccountIdentifier(developerLogin),
            PullRequestView = view,
            RequestOptions = options ?? RequestOptions.RequestOptionsDefault(),
            OperationName = "UpdateDataForPullRequestsAsync",
            Requestor = requestor ?? Guid.NewGuid(),
        };

        dynamic context = new ExpandoObject();
        var contextDict = (IDictionary<string, object>)context;
        contextDict.Add("Queries", string.Join(",", parameters.Uris));
        contextDict.Add("DeveloperLogin", developerLogin);
        contextDict.Add("Requestor", parameters.Requestor);
        contextDict.Add("View", view);

        try
        {
            await UpdateDataStoreAsync(parameters, UpdateDataForPullRequestsAsync);
        }
        catch (Exception ex)
        {
            contextDict.Add("ErrorMessage", ex.Message);
            SendErrorUpdateEvent(_log, this, parameters.Requestor, context, ex);
            return;
        }

        SendPullRequestUpdateEvent(_log, this, parameters.Requestor, context);
    }

    public Query? GetQuery(string queryId, string developerId)
    {
        ValidateDataStore();
        return Query.Get(DataStore, queryId, developerId);
    }

    public Query? GetQuery(AzureUri queryUri, string developerId)
    {
        if (!queryUri.IsQuery)
        {
            throw new ArgumentException($"Uri provided was not a Query Uri: {queryUri}");
        }

        return GetQuery(queryUri.Query, developerId);
    }

    public PullRequests? GetPullRequests(string organization, string project, string repositoryName, string developerId, PullRequestView view)
    {
        ValidateDataStore();
        return PullRequests.Get(DataStore, organization, project, repositoryName, developerId, view);
    }

    public PullRequests? GetPullRequests(AzureUri repositoryUri, string developerId, PullRequestView view)
    {
        if (!repositoryUri.IsRepository)
        {
            throw new ArgumentException($"Uri provided was not a Repository Uri: {repositoryUri}");
        }

        return GetPullRequests(repositoryUri.Organization, repositoryUri.Project, repositoryUri.Repository, developerId, view);
    }

    private async Task UpdateDataForQueriesAsync(DataStoreOperationParameters parameters)
    {
        _log.Debug($"Inside UpdateDataForQueriesAsync with Parameters: {parameters}");
        foreach (var uri in parameters.Uris)
        {
            if (!uri.IsQuery)
            {
                throw new ArgumentException($"Query is not a valid Uri Query: {uri}");
            }
        }

        if (parameters.DeveloperId == null)
        {
            throw new ArgumentException("DeveloperId was null");
        }

        if (parameters.RequestOptions == null)
        {
            throw new ArgumentException("RequestOptions was null");
        }

        // Iterate over and process each Uri in the set.
        foreach (var azureUri in parameters.Uris)
        {
            var result = GetConnection(azureUri.Connection, parameters.DeveloperId);
            if (result.Result != ResultType.Success)
            {
                if (result.Exception != null)
                {
                    throw result.Exception;
                }
                else
                {
                    throw new AzureAuthorizationException($"Failed getting connection: {azureUri.Connection} for {parameters.DeveloperId.LoginId} with {result.Error}");
                }
            }

            var witClient = result.Connection!.GetClient<WorkItemTrackingHttpClient>();
            if (witClient == null)
            {
                throw new AzureClientException($"Failed getting WorkItemTrackingHttpClient for {parameters.DeveloperId.LoginId} and {azureUri.Connection}");
            }

            // Good practice to only create data after we know the client is valid, but any exceptions
            // will roll back the transaction.
            var org = Organization.GetOrCreate(DataStore, azureUri.Connection);
            if (org == null)
            {
                throw new DataStoreException($"Organization.GetOrCreate failed for: {azureUri.Connection}");
            }

            var project = Project.Get(DataStore, azureUri.Project, org.Id);
            if (project is null)
            {
                var teamProject = GetTeamProject(azureUri.Project, parameters.DeveloperId, azureUri.Connection);
                project = Project.GetOrCreateByTeamProject(DataStore, teamProject, org.Id);
            }

            var getQueryResult = await witClient.GetQueryAsync(project.InternalId, azureUri.Query);
            if (getQueryResult == null)
            {
                throw new AzureClientException($"GetQueryAsync failed for {azureUri.Connection}, {project.InternalId}, {azureUri.Query}");
            }

            var queryId = new Guid(azureUri.Query);
            var queryResult = await witClient.QueryByIdAsync(project.InternalId, queryId);
            if (queryResult == null)
            {
                throw new AzureClientException($"QueryByIdAsync failed for {azureUri.Connection}, {project.InternalId}, {queryId}");
            }

            var workItemIds = new List<int>();

            // The WorkItems collection and individual reference objects may be null.
            switch (queryResult.QueryType)
            {
                // Tree types are treated as flat, but the data structure is different.
                case TFModels.QueryType.Tree:
                    if (queryResult.WorkItemRelations is not null)
                    {
                        foreach (var workItemRelation in queryResult.WorkItemRelations)
                        {
                            if (workItemRelation is null || workItemRelation.Target is null)
                            {
                                continue;
                            }

                            workItemIds.Add(workItemRelation.Target.Id);
                            if (workItemIds.Count >= QueryResultLimit)
                            {
                                break;
                            }
                        }
                    }

                    break;

                case TFModels.QueryType.Flat:
                    if (queryResult.WorkItems is not null)
                    {
                        foreach (var item in queryResult.WorkItems)
                        {
                            if (item is null)
                            {
                                continue;
                            }

                            workItemIds.Add(item.Id);
                            if (workItemIds.Count >= QueryResultLimit)
                            {
                                break;
                            }
                        }
                    }

                    break;

                case TFModels.QueryType.OneHop:

                    // OneHop work item structure is the same as the tree type.
                    goto case TFModels.QueryType.Tree;

                default:
                    _log.Warning($"Found unhandled QueryType: {queryResult.QueryType} for query: {queryId}");
                    break;
            }

            var workItems = new List<TFModels.WorkItem>();
            if (workItemIds.Count > 0)
            {
                workItems = await witClient.GetWorkItemsAsync(project.InternalId, workItemIds, null, null, TFModels.WorkItemExpand.Links, TFModels.WorkItemErrorPolicy.Omit);
                if (workItems == null)
                {
                    throw new AzureClientException($"GetWorkItemsAsync failed for {azureUri.Connection}, {project.InternalId}, Ids: {string.Join(",", workItemIds.ToArray())}");
                }
            }

            // Convert all work items to Json based on fields provided in RequestOptions.
            dynamic workItemsObj = new ExpandoObject();
            var workItemsObjDict = (IDictionary<string, object>)workItemsObj;
            foreach (var workItem in workItems)
            {
                dynamic workItemObj = new ExpandoObject();
                var workItemObjFields = (IDictionary<string, object>)workItemObj;

                // System.Id is excluded from the query result.
                workItemObjFields.Add(SystemIdFieldName, workItem.Id!);

                var htmlUrl = Links.GetLinkHref(workItem.Links, "html");
                workItemObjFields.Add(WorkItemHtmlUrlFieldName, htmlUrl);

                foreach (var field in parameters.RequestOptions.Fields)
                {
                    // Ensure we do not try to add duplicate fields.
                    if (workItemObjFields.ContainsKey(field))
                    {
                        _log.Warning($"Found duplicate field '{field} in RequestOptions.Fields: {string.Join(",", parameters.RequestOptions.Fields.ToArray())}");
                        continue;
                    }

                    if (!workItem.Fields.ContainsKey(field))
                    {
                        workItemObjFields.Add(field, string.Empty);
                        continue;
                    }

                    var fieldValue = workItem.Fields[field].ToString();
                    if (fieldValue is null)
                    {
                        workItemObjFields.Add(field, string.Empty);
                        continue;
                    }

                    if (workItem.Fields[field] is DateTime dateTime)
                    {
                        // If we have a datetime object, convert it to ticks for easy conversion
                        // to a DateTime object in whatever local format the user is in.
                        workItemObjFields.Add(field, dateTime.Ticks);
                        continue;
                    }

                    if (fieldValue == IdentityRefFieldValueName)
                    {
                        var identity = Identity.GetOrCreateIdentity(DataStore, workItem.Fields[field] as IdentityRef, result.Connection);
                        workItemObjFields.Add(field, identity);
                        continue;
                    }

                    if (field == WorkItemTypeFieldName)
                    {
                        var workItemType = WorkItemType.Get(DataStore, fieldValue, project.Id);
                        if (workItemType == null)
                        {
                            // Need a separate query to create WorkItemType object.
                            var workItemTypeInfo = await witClient!.GetWorkItemTypeAsync(project.InternalId, fieldValue);
                            workItemType = WorkItemType.GetOrCreateByTeamWorkItemType(DataStore, workItemTypeInfo, project.Id);
                        }

                        workItemObjFields.Add(field, workItemType);
                        continue;
                    }

                    workItemObjFields.Add(field, workItem.Fields[field].ToString()!);
                }

                workItemsObjDict.Add(workItem.Id.ToStringInvariant(), workItemObj);
            }

            JsonSerializerOptions serializerOptions = new()
            {
#if DEBUG
                WriteIndented = true,
#else
            WriteIndented = false,
#endif
            };

            var serializedJson = JsonSerializer.Serialize(workItemsObj, serializerOptions);
            Query.GetOrCreate(DataStore, azureUri.Query, project.Id, parameters.DeveloperId.LoginId, getQueryResult.Name, serializedJson, workItemIds.Count);
        } // Foreach AzureUri

        return;
    }

    private async Task UpdateDataForPullRequestsAsync(DataStoreOperationParameters parameters)
    {
        _log.Debug($"Inside UpdateDataForDeveloperPullRequestsAsync with Parameters: {parameters}");
        foreach (var uri in parameters.Uris)
        {
            if (!uri.IsRepository)
            {
                throw new ArgumentException($"Uri is not a valid Repository Uri: {uri}");
            }
        }

        if (parameters.DeveloperId == null)
        {
            throw new ArgumentException("DeveloperId was null");
        }

        if (parameters.RequestOptions == null)
        {
            throw new ArgumentException("RequestOptions was null");
        }

        if (parameters.PullRequestView == PullRequestView.Unknown)
        {
            throw new ArgumentException("PullRequestView is unknown");
        }

        // Iterate over and process each Uri in the set.
        foreach (var azureUri in parameters.Uris)
        {
            var result = GetConnection(azureUri.Connection, parameters.DeveloperId);
            if (result.Result != ResultType.Success)
            {
                if (result.Exception != null)
                {
                    throw result.Exception;
                }
                else
                {
                    throw new AzureAuthorizationException($"Failed getting connection: {azureUri.Connection} for {parameters.DeveloperId.LoginId} with {result.Error}");
                }
            }

            var gitClient = result.Connection!.GetClient<GitHttpClient>();
            if (gitClient == null)
            {
                throw new AzureClientException($"Failed getting GitHttpClient for {parameters.DeveloperId.LoginId} and {azureUri.Connection}");
            }

            var org = Organization.GetOrCreate(DataStore, azureUri.Connection);
            if (org == null)
            {
                throw new DataStoreException($"Organization.GetOrCreate failed for: {azureUri.Connection}");
            }

            var project = Project.Get(DataStore, azureUri.Project, org.Id);
            if (project is null)
            {
                var teamProject = GetTeamProject(azureUri.Project, parameters.DeveloperId, azureUri.Connection);
                project = Project.GetOrCreateByTeamProject(DataStore, teamProject, org.Id);
            }

            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = PullRequestStatus.Active,
                IncludeLinks = true,
            };

            // The view determines criteria parameters.
            switch (parameters.PullRequestView)
            {
                case PullRequestView.Unknown:
                    throw new ArgumentException("PullRequestView is unknown");
                case PullRequestView.Mine:
                    searchCriteria.CreatorId = result.Connection!.AuthorizedIdentity.Id;
                    break;
                case PullRequestView.Assigned:
                    searchCriteria.ReviewerId = result.Connection!.AuthorizedIdentity.Id;
                    break;
                case PullRequestView.All:
                    /* Nothing different for this */
                    break;
            }

            var pullRequests = await gitClient.GetPullRequestsAsync(project.InternalId, azureUri.Repository, searchCriteria, null, null, PullRequestResultLimit);
            if (pullRequests == null || pullRequests.Count == 0)
            {
                // If PRs were null or empty, this is a valid result, so create an empty record.
                PullRequests.GetOrCreate(DataStore, azureUri.Repository, project.Id, parameters.DeveloperId.LoginId, parameters.PullRequestView, new JsonObject().ToJsonString());
                return;
            }

            // Convert relevant pull request items to Json.
            dynamic pullRequestsObj = new ExpandoObject();
            var pullRequestsObjDict = (IDictionary<string, object>)pullRequestsObj;
            foreach (var pullRequest in pullRequests)
            {
                dynamic pullRequestObj = new ExpandoObject();
                var pullRequestObjFields = (IDictionary<string, object>)pullRequestObj;

                pullRequestObjFields.Add("Id", pullRequest.PullRequestId);
                pullRequestObjFields.Add("Title", pullRequest.Title);

                var creator = Identity.GetOrCreateIdentity(DataStore, pullRequest.CreatedBy, result.Connection);
                pullRequestObjFields.Add("CreatedBy", creator);
                pullRequestObjFields.Add("CreationDate", pullRequest.CreationDate.Ticks);
                pullRequestObjFields.Add("TargetBranch", pullRequest.TargetRefName);

                // The Links lack an html url for these results, construct the Url.
                var htmlUrl = $"{project.ConnectionUri}_git/{azureUri.Repository}/pullrequest/{pullRequest.PullRequestId}";
                pullRequestObjFields.Add("HtmlUrl", htmlUrl);

                pullRequestsObjDict.Add(pullRequest.PullRequestId.ToString(CultureInfo.InvariantCulture), pullRequestObj);
            }

            JsonSerializerOptions serializerOptions = new()
            {
#if DEBUG
                WriteIndented = true,
#else
            WriteIndented = false,
#endif
            };

            var serializedJson = JsonSerializer.Serialize(pullRequestsObj, serializerOptions);
            PullRequests.GetOrCreate(DataStore, azureUri.Repository, project.Id, parameters.DeveloperId.LoginId, parameters.PullRequestView, serializedJson);
        } // Foreach AzureUri

        return;
    }

    public TeamProject GetTeamProject(string projectName, DeveloperId.DeveloperId developerId, Uri connection)
    {
        var result = GetConnection(connection, developerId);
        if (result.Result != ResultType.Success)
        {
            if (result.Exception != null)
            {
                throw result.Exception;
            }
            else
            {
                throw new AzureAuthorizationException($"Failed getting connection: {connection} for {developerId.LoginId} with {result.Error}");
            }
        }

        var projectClient = new ProjectHttpClient(result.Connection!.Uri, result.Connection!.Credentials);
        if (projectClient == null)
        {
            throw new AzureClientException($"Failed getting ProjectHttpClient for {connection}");
        }

        var project = projectClient.GetProject(projectName).Result;
        if (project == null)
        {
            throw new AzureClientException($"Project reference was null for {connection} and Project: {projectName}");
        }

        return project;
    }

    // Wrapper for database operations for consistent handling.
    private async Task UpdateDataStoreAsync(DataStoreOperationParameters parameters, Func<DataStoreOperationParameters, Task> asyncAction)
    {
        parameters.RequestOptions ??= RequestOptions.RequestOptionsDefault();
        if (parameters.DeveloperId == null)
        {
            _log.Error($"Specified DeveloperId was not found: {parameters.LoginId}");
            throw new ArgumentException($"Specified DeveloperId was not found:");
        }

        using var tx = DataStore.Connection!.BeginTransaction();

        try
        {
            // Do the actions that modify the DataStore.
            await asyncAction(parameters);

            // Clean datastore and set last updated after updating.
            PruneObsoleteData();
            SetLastUpdatedInMetaData();
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Failed Updating DataStore for: {parameters}");
            tx.Rollback();

            // Rethrow so clients can catch/display an error UX.
            throw;
        }

        tx.Commit();
        _log.Debug($"Updated datastore: {parameters}");
    }

    private static void SendQueryUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Query, requestor, context);
    }

    private static void SendPullRequestUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.PullRequest, requestor, context);
    }

    private static void SendErrorUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception ex)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Error, requestor, context, ex);
    }

    private static void SendUpdateEvent(ILogger logger, object? source, DataManagerUpdateKind kind, Guid requestor, dynamic context, Exception? ex = null)
    {
        if (OnUpdate != null)
        {
            logger.Debug($"Sending Update Event for {requestor}  Kind: {kind}  Context: {string.Join(",", context)}");
            OnUpdate.Invoke(source, new DataManagerUpdateEventArgs(kind, requestor, context, ex));
        }
    }

    // Connections can go bad and throw VssUnauthorizedException after some time, even if
    // if HasAuthenticated is true. The forceNewConnection default parameter is set to true until
    // the bad connection issue can be reliably detected and solved.
    public static ConnectionResult GetConnection(Uri connectionUri, DeveloperId.DeveloperId developerId, bool forceNewConnection = true)
    {
        var log = Log.ForContext("SourceContext", Name);
        VssConnection? connection;
        var connectionKey = Tuple.Create(connectionUri, developerId);
        if (Connections.ContainsKey(connectionKey))
        {
            if (Connections.TryGetValue(connectionKey, out connection))
            {
                // If not forcing a new connection and it has authenticated, reuse it.
                if (!forceNewConnection && connection.HasAuthenticated)
                {
                    log.Debug($"Retrieving valid connection to {connectionUri} with {developerId.LoginId}");
                    return new ConnectionResult(connectionUri, null, connection);
                }
                else
                {
                    // Remove the bad connection.
                    if (Connections.TryRemove(new KeyValuePair<Tuple<Uri, DeveloperId.DeveloperId>, VssConnection>(connectionKey, connection)))
                    {
                        log.Debug($"Removed bad connection to {connectionUri} with {developerId.LoginId}");
                    }
                    else
                    {
                        // Something else may have removed it first, that's probably OK, but log it
                        // in case it isn't as it may be a symptom of a larger problem.
                        log.Warning($"Failed to remove bad connection to {connectionUri} with {developerId.LoginId}");
                    }
                }
            }
        }

        // Either connection was bad or it doesn't exist.
        var result = AzureClientProvider.CreateVssConnection(connectionUri, developerId);
        if (result.Result == ResultType.Success)
        {
            if (Connections.TryAdd(connectionKey, result.Connection!))
            {
                log.Debug($"Added connection for {connectionUri} with {developerId.LoginId}");
            }
            else
            {
                log.Warning($"Failed to add connection for {connectionUri} with {developerId.LoginId}");
            }
        }

        return result;
    }

    // Removes unused data from the datastore.
    private void PruneObsoleteData()
    {
        Query.DeleteBefore(DataStore, DateTime.UtcNow - DataRetentionTime);
        PullRequests.DeleteBefore(DataStore, DateTime.UtcNow - DataRetentionTime);
        WorkItemType.DeleteBefore(DataStore, DateTime.UtcNow - DataRetentionTime);
        Identity.DeleteBefore(DataStore, DateTime.UtcNow - DataRetentionTime);

        // The following are not yet pruned, need to ensure there are no current key references
        // before deletion.
        // * Projects are referenced by Pull Requests and Queries.
        // * Organizations are referenced by Projects.
    }

    // Sets a last-updated in the MetaData.
    private void SetLastUpdatedInMetaData()
    {
        MetaData.AddOrUpdate(DataStore, LastUpdatedKeyName, DateTime.Now.ToDataStoreString());
    }

    private void ValidateDataStore()
    {
        if (DataStore is null || !DataStore.IsConnected)
        {
            throw new DataStoreInaccessibleException("DataStore is not available.");
        }
    }

    private void HandleDeveloperIdChange(IDeveloperIdProvider sender, IDeveloperId args)
    {
        if (DeveloperIdProvider.GetInstance().GetDeveloperIdState(args) == AuthenticationState.LoggedOut)
        {
            // If a DeveloperId change happens and it is a removal, then we should delete the datastore
            // and remove the data that will be useless and inaccessible to the user. Since the datastore
            // likely cannot be deleted while it is in use and fail with a sharing violation, we will
            // instead mark a setting that will have the datastore recreated on next launch.
            try
            {
                _log.Information($"DeveloperId has logged out, marking DataStore for recreation on next launch.");
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[RecreateDataStoreSettingsKey] = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Failed setting Recreate Data Store setting.");
            }
        }
    }

    // Making the default options a singleton to avoid repeatedly calling the storage APIs and
    // creating a new AzureDataStoreSchema when not necessary.
    private static readonly Lazy<DataStoreOptions> LazyDataStoreOptions = new(DefaultOptionsInit);

    public static DataStoreOptions DefaultOptions => LazyDataStoreOptions.Value;

    private static DataStoreOptions DefaultOptionsInit()
    {
        return new DataStoreOptions
        {
            DataStoreFolderPath = ApplicationData.Current.LocalFolder.Path,
            DataStoreSchema = new AzureDataStoreSchema(),
        };
    }

    private bool disposed; // To detect redundant calls.

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                try
                {
                    _log.Debug("Disposing of all Disposable resources.");
                    if (Instances.TryGetValue(InstanceName, out var instanceName) && instanceName == UniqueName)
                    {
                        Instances.TryRemove(InstanceName, out _);
                        _log.Information($"Removed AzureDataManager: {UniqueName}.");
                    }

                    DataStore?.Dispose();
                }
                catch
                {
                }
            }

            disposed = true;
        }
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
