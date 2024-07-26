// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public enum PolicyStatus
{
    // Sorted by severity. Most severe is lowest.
    Broken = 1,
    Rejected = 2,
    Queued = 3,
    Running = 4,
    Approved = 5,
    NotApplicable = 6,
    Unknown = 7,
}
