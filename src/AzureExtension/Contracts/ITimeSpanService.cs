// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.DevBox;

namespace AzureExtension.Contracts;

/// <summary>
/// Interface that an operation watcher can use to get the time span based on the action to perform.
/// </summary>
public interface ITimeSpanService
{
    TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform);

    TimeSpan DevBoxOperationDeadline { get; }
}
