// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.DevBox;

namespace DevHomeAzureExtension.Contracts;

/// <summary>
/// Interface that an operation watcher can use to get the time span based on the action to perform.
/// </summary>
public interface ITimeSpanService
{
    TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform);

    TimeSpan DevBoxOperationDeadline { get; }
}
