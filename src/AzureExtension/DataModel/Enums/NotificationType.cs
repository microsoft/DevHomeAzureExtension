// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public enum NotificationType
{
    Unknown = 0,
    PullRequestApproved = 1,
    PullRequestRejected = 2,
    NewReview = 3,
}
