// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureExtension.Contracts;
using AzureExtension.DevBox;

namespace AzureExtension.Test.DevBox;

public class TimeSpanServiceMock : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline => TimeSpan.FromSeconds(3);

    public TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform) => TimeSpan.FromSeconds(1);
}
