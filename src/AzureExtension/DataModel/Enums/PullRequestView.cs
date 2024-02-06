// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public enum PullRequestView
{
    Unknown,

    /// <summary>
    /// View all pull requests.
    /// </summary>
    All,

    /// <summary>
    /// View my pull requests.
    /// </summary>
    Mine,

    /// <summary>
    /// View pull requests assigned to me.
    /// </summary>
    Assigned,
}
