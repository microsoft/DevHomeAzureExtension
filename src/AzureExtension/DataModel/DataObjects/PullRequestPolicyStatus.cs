// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("PullRequestPolicyStatus")]
public class PullRequestPolicyStatus
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(PullRequestPolicyStatus)}"));

    private static ILogger Log => _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // This should suffice as a unique identifier, as it is the Project Guid + PullRequestId
    public string ArtifactId { get; set; } = string.Empty;

    // Key in Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    // Key in Repository table
    public long RepositoryId { get; set; } = DataStore.NoForeignKey;

    public long PullRequestId { get; set; } = DataStore.NoForeignKey;

    public string Title { get; set; } = string.Empty;

    public long PolicyStatusId { get; set; } = DataStore.NoForeignKey;

    public string PolicyStatusReason { get; set; } = string.Empty;

    public long PullRequestStatusId { get; set; } = DataStore.NoForeignKey;

    public string TargetBranchName { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;

    // Time of the last pull request update (push), if it exists.
    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    // Time the pull request was created.
    public long TimeCreated { get; set; } = DataStore.NoForeignKey;

    public override string ToString() => ArtifactId;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public Repository Repository => Repository.Get(DataStore, RepositoryId);

    [Write(false)]
    [Computed]
    public DateTime CreatedAt => TimeCreated.ToDateTime();

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    [Write(false)]
    [Computed]
    public PolicyStatus PolicyStatus => (PolicyStatus)PolicyStatusId;

    [Write(false)]
    [Computed]
    public PullRequestStatus Status => (PullRequestStatus)PullRequestStatusId;

    [Write(false)]
    [Computed]
    public bool CompletedOrAbandoned => (Status == PullRequestStatus.Completed) || (Status == PullRequestStatus.Abandoned);

    [Write(false)]
    [Computed]
    public bool ActiveOrNotSet => (Status == PullRequestStatus.Active) || (Status == PullRequestStatus.NotSet);

    [Write(false)]
    [Computed]
    public bool Waiting => (PolicyStatus == PolicyStatus.Queued) || (PolicyStatus == PolicyStatus.Running);

    [Write(false)]
    [Computed]
    public bool Rejected => PolicyStatus <= PolicyStatus.Rejected;

    [Write(false)]
    [Computed]
    public bool Approved => PolicyStatus >= PolicyStatus.Approved;

    // Map PolicyEvaluationStatus to our enum. Our enum is sorted, but the PolicyEvaluationStatus is not.
    public static PolicyStatus GetFromPolicyEvaluationStatus(PolicyEvaluationStatus? policyEvaluationStatus)
    {
        return policyEvaluationStatus switch
        {
            PolicyEvaluationStatus.NotApplicable => PolicyStatus.NotApplicable,
            PolicyEvaluationStatus.Approved => PolicyStatus.Approved,
            PolicyEvaluationStatus.Rejected => PolicyStatus.Rejected,
            PolicyEvaluationStatus.Queued => PolicyStatus.Queued,
            PolicyEvaluationStatus.Running => PolicyStatus.Running,
            PolicyEvaluationStatus.Broken => PolicyStatus.Broken,
            _ => PolicyStatus.Unknown,
        };
    }

    public static PullRequestPolicyStatus Create(GitPullRequest pullRequest, string artifactId, long projectId, long repositoryId, PolicyStatus policyStatus, string reason, string htmlUrl)
    {
        // Get Current status of this pull request, and create a summary capture.
        var status = new PullRequestPolicyStatus
        {
            ArtifactId = artifactId,
            ProjectId = projectId,
            RepositoryId = repositoryId,
            PullRequestId = pullRequest.PullRequestId,
            Title = pullRequest.Title,
            PolicyStatusId = (long)policyStatus,
            PolicyStatusReason = reason,
            TargetBranchName = pullRequest.TargetRefName,
            PullRequestStatusId = (long)pullRequest.Status,
            HtmlUrl = htmlUrl,
            TimeCreated = pullRequest.CreationDate.ToUniversalTime().ToDataStoreInteger(),
        };

        // Set TimeUpdated to be the most recent commit push time if it exists.
        if (pullRequest.LastMergeSourceCommit?.Push is not null)
        {
            status.TimeUpdated = pullRequest.LastMergeSourceCommit.Push.Date.ToUniversalTime().ToDataStoreInteger();
            if (status.TimeUpdated < status.TimeCreated)
            {
                // First commits will often be pushed before the pull request is created. In this case,
                // we want the TimeUpdated to be at a minimum the TimeCreated.
                status.TimeUpdated = status.TimeCreated;
            }
        }
        else
        {
            status.TimeUpdated = status.TimeCreated;
        }

        return status;
    }

    public static PullRequestPolicyStatus? Get(DataStore dataStore, string artifactId)
    {
        var sql = @"SELECT * FROM PullRequestPolicyStatus WHERE ArtifactId = @ArtifactId ORDER BY TimeCreated DESC LIMIT 1;";
        var param = new
        {
            ArtifactId = artifactId,
        };

        Log.Verbose(DataStore.GetSqlLogMessage(sql, param));
        var pullRequestStatus = dataStore.Connection!.QueryFirstOrDefault<PullRequestPolicyStatus>(sql, param, null);
        if (pullRequestStatus is not null)
        {
            // Add Datastore so this object can make internal queries.
            pullRequestStatus.DataStore = dataStore;
        }

        return pullRequestStatus;
    }

    public static PullRequestPolicyStatus Add(DataStore dataStore, GitPullRequest pullRequest, string artifactId, long projectId, long repositoryId, PolicyStatus policyStatus, string reason, string htmlUrl)
    {
        var pullRequestStatus = Create(pullRequest, artifactId, projectId, repositoryId, policyStatus, reason, htmlUrl);
        pullRequestStatus.Id = dataStore.Connection!.Insert(pullRequestStatus);
        Log.Debug($"Inserted PullRequestPolicyStatus, Id = {pullRequestStatus.Id}");

        // Remove older records we no longer need.
        DeleteOutdatedForPullRequest(dataStore, artifactId);

        pullRequestStatus.DataStore = dataStore;
        return pullRequestStatus;
    }

    public static void DeleteOutdatedForPullRequest(DataStore dataStore, string artifactId)
    {
        // Delete any records beyond the most recent 2.
        var sql = @"DELETE FROM PullRequestPolicyStatus WHERE ArtifactId = $Id AND Id NOT IN (SELECT Id FROM PullRequestPolicyStatus WHERE ArtifactId = $Id ORDER BY TimeCreated DESC LIMIT 2)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Id", artifactId);
        Log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        Log.Verbose(DataStore.GetDeletedLogMessage(rowsDeleted));
    }

    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        // Delete notifications older than the date listed.
        var sql = @"DELETE FROM PullRequestPolicyStatus WHERE TimeUpdated < $Time;";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        Log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        Log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
