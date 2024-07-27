// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataModel;

namespace DevHomeAzureExtension;

public interface IAzureDataManager : IDisposable
{
    DataStoreOptions DataStoreOptions { get; }

    DateTime LastUpdated { get; }

    string GetMetaData(string key);

    void SetMetaData(string key, string value);

    Task UpdateDataForAccountsAsync(RequestOptions? options = null, Guid? requestor = null);

    Task UpdateDataForAccountsAsync(TimeSpan olderThan, RequestOptions? options = null, Guid? requestor = null);

    Task UpdateDataForQueryAsync(AzureUri queryUri, string developerLogin, RequestOptions? options = null, Guid? requestor = null);

    Task UpdateDataForQueriesAsync(IEnumerable<AzureUri> queryUris, string developerLogin, RequestOptions? options = null, Guid? requestor = null);

    Task UpdateDataForPullRequestsAsync(AzureUri repositoryUri, string developerLogin, PullRequestView view, RequestOptions? options = null, Guid? requestor = null);

    Task UpdatePullRequestsForLoggedInDeveloperIdsAsync(RequestOptions? options = null, Guid? requestor = null);

    Query? GetQuery(string queryId, string developerId);

    Query? GetQuery(AzureUri queryUri, string developerId);

    Identity GetIdentity(long id);

    WorkItemType GetWorkItemType(long id);

    IEnumerable<Notification> GetNotifications(DateTime? since = null, bool includeToasted = false);

    IEnumerable<Repository> GetRepositories();

    IEnumerable<Repository> GetDeveloperRepositories();

    IEnumerable<PullRequests> GetPullRequestsForLoggedInDeveloperIds();

    // Repository name may not be unique across projects, and projects may not be unique across
    // organizations, so we need all three to identify the repository.
    PullRequests? GetPullRequests(string organization, string project, string repositoryName, string developerId, PullRequestView view);

    PullRequests? GetPullRequests(AzureUri repositoryUri, string developerId, PullRequestView view);
}
