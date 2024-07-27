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
using Microsoft.TeamFoundation.Policy.WebApi;
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
    public static readonly int QueryResultLimit = 25;

    // Most data that has not been updated within this time will be removed.
    private static readonly TimeSpan _dataRetentionTime = TimeSpan.FromDays(1);

    // This is how long we will keep a notification in the datastore.
    private static readonly TimeSpan _notificationRetentionTime = TimeSpan.FromDays(7);

    // Pull requests without a push in this amount of time are considered too old to
    // generate new notifications.
    private static readonly TimeSpan _pullRequestIsAncientTime = TimeSpan.FromDays(14);

    // The amount of time we will retain a pull request status record. If a very old pull
    // request is resurrected and updated beyond this time it will be treated as a new
    // pull request as the previous status record will be gone.
    private static readonly TimeSpan _pullRequestStatusRetentionTime = TimeSpan.FromDays(30);

    private static readonly string _lastUpdatedKeyName = "LastUpdated";

    private static readonly string _name = nameof(AzureDataManager);

    private static readonly ConcurrentDictionary<string, Guid> _instances = new();

    // Connections are a pairing of DeveloperId and a Uri.
    private static readonly ConcurrentDictionary<Tuple<Uri, DeveloperId.DeveloperId>, VssConnection> _connections = new();

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
            var log = Log.ForContext("SourceContext", _name);
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
        _log = Log.ForContext("SourceContext", $"{_name}/{InstanceName}");
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

        if (_instances.TryGetValue(InstanceName, out var instanceIdentifier))
        {
            // We should not have duplicate AzureDataManagers, as every client should have one,
            // but the identifiers may not be unique if using partial Guids. Note in the log
            // the duplicate as a warning and the existing unique name so we can see in the log what
            // client created the duplicate in order to discern random chance / consistent pattern.
            _log.Warning($"Duplicate instance created for identifier {InstanceName}:{instanceIdentifier}.");
        }
        else
        {
            _instances.TryAdd(InstanceName, UniqueName);
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
            var lastUpdated = MetaData.Get(DataStore, _lastUpdatedKeyName);
            if (lastUpdated == null)
            {
                return DateTime.MinValue;
            }

            return lastUpdated.ToDateTime();
        }
    }

    public override string ToString() => $"{_name}/{InstanceName}";

    public async Task UpdateDataForQueriesAsync(IEnumerable<AzureUri> queryUris, string developerLogin, RequestOptions? options = null, Guid? requestor = null)
    {
        ValidateDataStore();
        var parameters = new DataStoreOperationParameters
        {
            Uris = queryUris,
            LoginId = developerLogin,
            DeveloperId = DeveloperIdProvider.GetInstance().GetDeveloperIdFromAccountIdentifier(developerLogin),
            RequestOptions = options ?? RequestOptions.RequestOptionsDefault(),
            OperationName = nameof(UpdateDataForQueriesAsync),
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
            OperationName = nameof(UpdateDataForPullRequestsAsync),
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

    public Identity GetIdentity(long id)
    {
        ValidateDataStore();
        return Identity.Get(DataStore, id);
    }

    public WorkItemType GetWorkItemType(long id)
    {
        ValidateDataStore();
        return WorkItemType.Get(DataStore, id);
    }

    public IEnumerable<Notification> GetNotifications(DateTime? since = null, bool includeToasted = false)
    {
        ValidateDataStore();
        return Notification.Get(DataStore, since, includeToasted);
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
            var count = await witClient.GetQueryResultCountAsync(project.Name, queryId);
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
                        workItemObjFields.Add(field, identity.Id);
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

                        workItemObjFields.Add(field, workItemType.Id);
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
            Query.GetOrCreate(DataStore, azureUri.Query, project.Id, parameters.DeveloperId.LoginId, getQueryResult.Name, serializedJson, count);
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
            var connectionResult = GetConnection(azureUri.Connection, parameters.DeveloperId);
            if (connectionResult.Result != ResultType.Success)
            {
                if (connectionResult.Exception != null)
                {
                    throw connectionResult.Exception;
                }
                else
                {
                    throw new AzureAuthorizationException($"Failed getting connection: {azureUri.Connection} for {parameters.DeveloperId.LoginId} with {connectionResult.Error}");
                }
            }

            var gitClient = connectionResult.Connection!.GetClient<GitHttpClient>();
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

            var repository = Repository.Get(DataStore, project.Id, azureUri.Repository);
            if (repository is null)
            {
                var gitRepository = await gitClient.GetRepositoryAsync(project.InternalId, azureUri.Repository);
                if (gitRepository is null)
                {
                    throw new RepositoryNotFoundException(azureUri.Repository);
                }

                repository = Repository.GetOrCreate(DataStore, gitRepository, project.Id);
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
                    searchCriteria.CreatorId = connectionResult.Connection!.AuthorizedIdentity.Id;
                    break;
                case PullRequestView.Assigned:
                    searchCriteria.ReviewerId = connectionResult.Connection!.AuthorizedIdentity.Id;
                    break;
                case PullRequestView.All:
                    /* Nothing different for this */
                    break;
            }

            var pullRequests = await gitClient.GetPullRequestsAsync(project.InternalId, repository.InternalId, searchCriteria, null, null, PullRequestResultLimit);
            await ProcessPullRequests(
                connectionResult.Connection!,
                pullRequests,
                project,
                repository,
                parameters.DeveloperId.LoginId,
                parameters.PullRequestView,
                parameters.OperationName == nameof(UpdateDataForDeveloperPullRequestsAsync));
        } // Foreach AzureUri

        return;
    }

    public IEnumerable<PullRequests> GetPullRequestsForLoggedInDeveloperIds()
    {
        ValidateDataStore();
        return PullRequests.GetAllForDeveloper(DataStore);
    }

    public async Task UpdatePullRequestsForLoggedInDeveloperIdsAsync(RequestOptions? options = null, Guid? requestor = null)
    {
        ValidateDataStore();
        var parameters = new DataStoreOperationParameters
        {
            RequestOptions = options ?? RequestOptions.RequestOptionsDefault(),
            OperationName = nameof(UpdatePullRequestsForLoggedInDeveloperIdsAsync),
            PullRequestView = PullRequestView.Mine,
            Requestor = requestor ?? Guid.NewGuid(),
        };

        dynamic context = new ExpandoObject();
        var contextDict = (IDictionary<string, object>)context;
        contextDict.Add("Requestor", parameters.Requestor);

        try
        {
            await UpdateDataStoreAsync(parameters, UpdateDataForDeveloperPullRequestsAsync);
        }
        catch (Exception ex)
        {
            contextDict.Add("ErrorMessage", ex.Message);
            SendErrorUpdateEvent(_log, this, parameters.Requestor, context, ex);
            return;
        }

        SendDeveloperUpdateEvent(_log, this, parameters.Requestor, context);
    }

    private async Task UpdateDataForDeveloperPullRequestsAsync(DataStoreOperationParameters parameters)
    {
        _log.Debug($"Inside UpdateDataForDeveloperPullRequestsAsync with Parameters: {parameters}");

        // This is a loop over a subset of repositories with a specific developer ID and pull request view specified.
        var repositoryReferences = RepositoryReference.GetAll(DataStore);
        foreach (var repositoryRef in repositoryReferences)
        {
            var uri = new AzureUri(repositoryRef.Repository.CloneUrl);
            var uris = new List<AzureUri>
            {
                new(repositoryRef.Repository.CloneUrl),
            };

            var suboperationParameters = new DataStoreOperationParameters
            {
                Uris = uris,
                DeveloperId = repositoryRef.Developer.DeveloperId,
                RequestOptions = parameters.RequestOptions,
                OperationName = nameof(UpdateDataForDeveloperPullRequestsAsync),
                PullRequestView = PullRequestView.Mine,
                Requestor = parameters.Requestor,
            };

            await UpdateDataForPullRequestsAsync(suboperationParameters);
        }
    }

    private async Task ProcessPullRequests(
        VssConnection connection,
        List<GitPullRequest>? pullRequests,
        Project project,
        Repository repository,
        string loginId,
        PullRequestView view,
        bool isDeveloper)
    {
        if (pullRequests is null || pullRequests.Count == 0)
        {
            // If PRs were null or empty, this is a valid result, so create an empty record.
            PullRequests.GetOrCreate(DataStore, repository.Id, project.Id, loginId, view, new JsonObject().ToJsonString());
            return;
        }

        dynamic pullRequestsObj = new ExpandoObject();
        var pullRequestsObjDict = (IDictionary<string, object>)pullRequestsObj;
        var policyClient = connection.GetClient<PolicyHttpClient>();

        foreach (var pullRequest in pullRequests)
        {
            var status = PolicyStatus.Unknown;
            var statusReason = string.Empty;

            // ArtifactId is null in the pull request object and it is not the correct object. The ArtifactId for the
            // Policy Evaluations API is this:
            //     vstfs:///CodeReview/CodeReviewId/{projectId}/{pullRequestId}
            // Documentation: https://learn.microsoft.com/en-us/dotnet/api/microsoft.teamfoundation.policy.webapi.policyevaluationrecord.artifactid
            var artifactId = $"vstfs:///CodeReview/CodeReviewId/{project.InternalId}/{pullRequest.PullRequestId}";

            // Url in the GitPullRequest object is a REST Api Url, and the links lack an html Url, so we must build it.
            var htmlUrl = $"{repository.CloneUrl}/pullrequest/{pullRequest.PullRequestId}";

            try
            {
                var policyEvaluations = await policyClient.GetPolicyEvaluationsAsync(project.InternalId, artifactId);
                GetPolicyStatus(policyEvaluations, out status, out statusReason);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Failed getting policy evaluations for pull request: {pullRequest.PullRequestId} {pullRequest.Url}");
            }

            if (isDeveloper)
            {
                // Pull requests do not fully populate commit information. If this is a developer pull request, fetch the
                // additional commit information about the last merged source to determine when the last time the pull request
                // was pushed a commit.
                if (pullRequest.LastMergeSourceCommit is not null)
                {
                    var gitClient = connection.GetClient<GitHttpClient>();
                    if (gitClient is not null)
                    {
                        var commitRef = await gitClient.GetCommitAsync(pullRequest.LastMergeSourceCommit.CommitId, repository.InternalId);
                        if (commitRef is not null)
                        {
                            pullRequest.LastMergeSourceCommit = commitRef;
                        }
                    }
                }

                CreatePullRequestStatus(pullRequest, artifactId, project.Id, repository.Id, status, statusReason, htmlUrl);
            }

            dynamic pullRequestObj = new ExpandoObject();
            var pullRequestObjFields = (IDictionary<string, object>)pullRequestObj;

            pullRequestObjFields.Add("Id", pullRequest.PullRequestId);
            pullRequestObjFields.Add("Title", pullRequest.Title);
            pullRequestObjFields.Add("RepositoryId", repository.Id);
            pullRequestObjFields.Add("Status", pullRequest.Status);
            pullRequestObjFields.Add("PolicyStatus", status.ToString());
            pullRequestObjFields.Add("PolicyStatusReason", statusReason);

            var creator = Identity.GetOrCreateIdentity(DataStore, pullRequest.CreatedBy, connection);
            pullRequestObjFields.Add("CreatedBy", creator.Id);
            pullRequestObjFields.Add("CreationDate", pullRequest.CreationDate.Ticks);
            pullRequestObjFields.Add("TargetBranch", pullRequest.TargetRefName);
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
        PullRequests.GetOrCreate(DataStore, repository.Id, project.Id, loginId, view, serializedJson);
    }

    // Gets PolicyStatus and reason for a given list of PolicyEvaluationRecords
    private void GetPolicyStatus(List<PolicyEvaluationRecord> policyEvaluations, out PolicyStatus status, out string statusReason)
    {
        status = PolicyStatus.Unknown;
        statusReason = string.Empty;

        if (policyEvaluations != null)
        {
            var countApplicablePolicies = 0;
            foreach (var policyEvaluation in policyEvaluations)
            {
                if (policyEvaluation.Configuration.IsEnabled && policyEvaluation.Configuration.IsBlocking)
                {
                    ++countApplicablePolicies;
                    var evalStatus = PullRequestPolicyStatus.GetFromPolicyEvaluationStatus(policyEvaluation.Status);
                    if (evalStatus < status)
                    {
                        statusReason = policyEvaluation.Configuration.Type.DisplayName;
                        status = evalStatus;
                    }
                }
            }

            if (countApplicablePolicies == 0)
            {
                // If there is no applicable policy, treat the policy status as Approved.
                status = PolicyStatus.Approved;
            }
        }
    }

    private void CreatePullRequestStatus(GitPullRequest pullRequest, string artifactId, long projectId, long repositoryId, PolicyStatus status, string reason, string htmlUrl)
    {
        _log.Debug($"PullRequest: {pullRequest.PullRequestId}  Status: {status}  Reason: {reason}");
        var prevStatus = PullRequestPolicyStatus.Get(DataStore, artifactId);
        var curStatus = PullRequestPolicyStatus.Add(DataStore, pullRequest, artifactId, projectId, repositoryId, status, reason, htmlUrl);

        if (ShouldCreateRejectedNotification(curStatus, prevStatus))
        {
            _log.Information($"Creating Rejected Notification for {curStatus}");
            Notification.Create(DataStore, curStatus, NotificationType.PullRequestRejected);
        }

        if (ShouldCreateApprovalNotification(curStatus, prevStatus))
        {
            _log.Information($"Creating Approved Notification for {curStatus}");
            Notification.Create(DataStore, curStatus, NotificationType.PullRequestApproved);
        }
    }

    private bool ShouldCreateRejectedNotification(PullRequestPolicyStatus curStatus, PullRequestPolicyStatus? prevStatus)
    {
        // If the pull request is not recently updated, ignore it. This is to prevent ancient pull requests
        // from showing notifications.
        if ((DateTime.UtcNow - curStatus.UpdatedAt) > _pullRequestIsAncientTime)
        {
            return false;
        }

        // If the Pull Request is completed or abandoned then there is nothing to do.
        if (curStatus.CompletedOrAbandoned)
        {
            return false;
        }

        // Compare pull request status.
        if (prevStatus is null)
        {
            // No previous status for this commit, assume new PR or freshly pushed commit with
            // checks likely running. Any check failures here are assumed to be notification worthy.
            if (curStatus.Rejected)
            {
                return true;
            }
        }
        else
        {
            // A failure isn't necessarily notification worthy if we've already seen it.
            // We do not wish to spam the user with failure notifications.
            if (curStatus.Rejected)
            {
                // If the previous status was not failed, or the failure was for a different
                // reason, then create a new notification.
                if (!prevStatus.Rejected || (curStatus.PolicyStatusReason != prevStatus.PolicyStatusReason))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ShouldCreateApprovalNotification(PullRequestPolicyStatus curStatus, PullRequestPolicyStatus? prevStatus)
    {
        // If the pull request is not recently updated, ignore it. This is to prevent ancient pull requests
        // from showing notifications.
        if ((DateTime.UtcNow - curStatus.UpdatedAt) > _pullRequestIsAncientTime)
        {
            return false;
        }

        // If the Pull Request is completed or abandoned then there is nothing to do.
        if (curStatus.CompletedOrAbandoned)
        {
            return false;
        }

        // Compare pull request status.
        if (prevStatus is null)
        {
            // No previous status for this PR, it may have been approved between updates.
            if (curStatus.Approved)
            {
                return true;
            }
        }
        else
        {
            // Only post success notifications if it wasn't previously successful.
            // We do not wish to spam the user with success notifications.
            if (curStatus.Approved && !prevStatus.Approved)
            {
                return true;
            }
        }

        return false;
    }

    private TeamProject GetTeamProject(string projectName, DeveloperId.DeveloperId developerId, Uri connection)
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

    private static void SendDeveloperUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception? ex = null)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Developer, requestor, context, ex);
    }

    private static void SendCancelUpdateEvent(ILogger logger, object? source, Guid requestor, dynamic context, Exception? ex = null)
    {
        SendUpdateEvent(logger, source, DataManagerUpdateKind.Cancel, requestor, context, ex);
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
        var log = Log.ForContext("SourceContext", _name);
        VssConnection? connection;
        var connectionKey = Tuple.Create(connectionUri, developerId);
        if (_connections.ContainsKey(connectionKey))
        {
            if (_connections.TryGetValue(connectionKey, out connection))
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
                    if (_connections.TryRemove(new KeyValuePair<Tuple<Uri, DeveloperId.DeveloperId>, VssConnection>(connectionKey, connection)))
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
            if (_connections.TryAdd(connectionKey, result.Connection!))
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

    public IEnumerable<Repository> GetRepositories()
    {
        ValidateDataStore();
        return Repository.GetAll(DataStore);
    }

    public IEnumerable<Repository> GetDeveloperRepositories()
    {
        ValidateDataStore();
        return Repository.GetAllWithReference(DataStore);
    }

    public string GetMetaData(string key)
    {
        var metaData = MetaData.GetByKey(DataStore, key);
        if (metaData is null)
        {
            return string.Empty;
        }
        else
        {
            return metaData.Value;
        }
    }

    public void SetMetaData(string key, string value)
    {
        MetaData.AddOrUpdate(DataStore, key, value);
    }

    // Removes unused data from the datastore.
    private void PruneObsoleteData()
    {
        Query.DeleteBefore(DataStore, DateTime.UtcNow - _dataRetentionTime);
        PullRequests.DeleteBefore(DataStore, DateTime.UtcNow - _dataRetentionTime);
        WorkItemType.DeleteBefore(DataStore, DateTime.UtcNow - _dataRetentionTime);
        Notification.DeleteBefore(DataStore, DateTime.UtcNow - _notificationRetentionTime);
        PullRequestPolicyStatus.DeleteBefore(DataStore, DateTime.UtcNow - _pullRequestStatusRetentionTime);

        // The following are not yet pruned, need to ensure there are no current key references
        // before deletion.
        // * Projects are referenced by Pull Requests and Queries.
        // * Organizations are referenced by Projects.
        // * Identity is referenced by ProjectReference and RepositoryReference
    }

    // Sets a last-updated in the MetaData.
    private void SetLastUpdatedInMetaData()
    {
        MetaData.AddOrUpdate(DataStore, _lastUpdatedKeyName, DateTime.UtcNow.ToDataStoreString());
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
    private static readonly Lazy<DataStoreOptions> _lazyDataStoreOptions = new(DefaultOptionsInit);

    public static DataStoreOptions DefaultOptions => _lazyDataStoreOptions.Value;

    private static DataStoreOptions DefaultOptionsInit()
    {
        return new DataStoreOptions
        {
            DataStoreFolderPath = ApplicationData.Current.LocalFolder.Path,
            DataStoreSchema = new AzureDataStoreSchema(),
        };
    }

    private bool _disposed; // To detect redundant calls.

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _log.Debug("Disposing of all Disposable resources.");
                    if (_instances.TryGetValue(InstanceName, out var instanceName) && instanceName == UniqueName)
                    {
                        _instances.TryRemove(InstanceName, out _);
                        _log.Information($"Removed AzureDataManager: {UniqueName}.");
                    }

                    DataStore?.Dispose();
                }
                catch
                {
                }
            }

            _disposed = true;
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
