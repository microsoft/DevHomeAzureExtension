﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using AzureExtension.DevBox.Models;

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// https://azure.github.io/typespec-azure/docs/howtos/Azure%20Core/long-running-operations
/// for move information on long running operations. This class is used to represent a long running operation.
/// </summary>
public class DevBoxLongRunningOperation : DevCenterOperationBase
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
