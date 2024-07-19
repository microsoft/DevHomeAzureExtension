// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapper;
using Dapper.Contrib.Extensions;
using DevHomeAzureExtension.Helpers;
using Microsoft.TeamFoundation.Policy.WebApi;
using Serilog;

namespace DevHomeAzureExtension.DataModel;

[Table("PullRequestPolicyStatus")]
public class PullRequestPolicyStatus
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", $"DataModel/{nameof(PullRequests)}"));

    private static readonly ILogger _log = _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    // This should suffice as a unique identifier.
    public string ArtifactId { get; set; } = string.Empty;

    // Key in Project table
    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public long PullRequestId { get; set; } = DataStore.NoForeignKey;

    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    public long PolicyStatusId { get; set; } = DataStore.NoForeignKey;

    public override string ToString() => ArtifactId;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public PolicyStatus PolicyStatus => (PolicyStatus)PolicyStatusId;

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

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
}
