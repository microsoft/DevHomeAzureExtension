// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox;

namespace DevHomeAzureExtension.Test.DevBox;

public class TimeSpanServiceMock : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline => TimeSpan.FromSeconds(3);

    public TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform) => TimeSpan.FromSeconds(1);
}
