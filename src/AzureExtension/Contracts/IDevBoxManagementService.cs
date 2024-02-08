// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

    Task<JsonElement> GetDevBoxesAsJsonAsync(string devCenterUri, string project);
}
