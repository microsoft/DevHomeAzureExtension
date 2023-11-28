// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AzureExtension.Contracts;
using DevHomeAzureExtension.DataModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class ManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    public ManagementService(IDevBoxAuthService authService) =>
        _authService = authService;

    private static readonly string Query = " ";

    public IDeveloperId? DevId
    {
        get; set;
    }

    public JsonElement GetAllProjectsAsJsonAsync()
    {
        JsonElement result = default;

        return result;
    }

    public JsonElement GetBoxesAsJsonAsync(string devCenterUri, string project)
    {
        JsonElement result = default;

        return result;
    }
}
