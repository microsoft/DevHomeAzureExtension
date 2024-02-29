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

/// <summary>
/// Service to provide time spans for different operations.
/// </summary>
public class TimeSpanService : ITimeSpanService
{
    public TimeSpan DevBoxOperationDeadline { get; private set; } = Constants.OperationDeadline;

    /// <summary>
    /// Get the period interval based on the action to perform.
    /// </summary>
    /// <remarks>
    /// The creation operation of a Dev Box takes longer than all other operations and can take between 20 to 65 minutes. Possibly more depending
    /// on how the users IT admin has configured their Dev Center subscriptions. All other operations take less than 5 minutes except for the delete
    /// operation which can take up to 10 minutes.
    /// </remarks>
    /// <param name="actionToPerform">The Dev Box action that is being performed. E.g Start</param>
    /// <returns>The TimeSpan value that the <see cref="DevBoxOperationWatcher"/>  can use to its periodic timer intervals</returns>
    public TimeSpan GetPeriodIntervalBasedOnAction(DevBoxActionToPerform actionToPerform)
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
