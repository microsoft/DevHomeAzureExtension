// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureExtension.Contracts;
using AzureExtension.DevBox;

namespace AzureExtension.Services.DevBox;

public class TimeSpanService : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline { get; private set; } = Constants.OperationDeadline;

    public TimeSpan GetTimeSpanBasedOnAction(DevBoxActionToPerform actionToPerform)
    {
        switch (actionToPerform)
        {
            case DevBoxActionToPerform.Create:
                return Constants.FiveMinutePeriod;
            default:
                return Constants.OneMinutePeriod;
        }
    }
}
