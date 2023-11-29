// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IDevBoxManagementService
{
    public IDeveloperId? DevId
    {
        get; set;
    }

    Task<JsonElement> GetAllProjectsAsJsonAsync();

    Task<JsonElement> GetBoxesAsJsonAsync(string devCenterUri, string project);
}
