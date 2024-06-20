// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using AzureExtension.DevBox;

namespace AzureExtension.Test.DevBox;

public class TimeSpanServiceMock : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline => TimeSpan.FromSeconds(3);

    public TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform) => TimeSpan.FromSeconds(1);
}
