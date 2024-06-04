// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox;

namespace DevHomeAzureExtension.Test.DevBox;

public class TimeSpanServiceMock : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline => TimeSpan.FromSeconds(3);

    public TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform) => TimeSpan.FromSeconds(1);
}
