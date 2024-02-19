// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureExtension.DevBox;

namespace AzureExtension.Contracts;

public interface ITimeSpanService
{
    TimeSpan GetTimeSpanBasedOnAction(DevBoxActionToPerform actionToPerform);

    TimeSpan DevBoxOperationDeadline { get; }
}
