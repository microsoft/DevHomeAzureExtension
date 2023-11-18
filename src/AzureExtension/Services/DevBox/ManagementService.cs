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

    private static readonly string Query =
    "{\"query\": \"Resources | where type in~ ('microsoft.devcenter/projects') | where properties['provisioningState'] =~ 'Succeeded' | project id, location, tenantId, name, properties, type\"," +
    " \"options\":{\"allowPartialScopes\":true}}";

    public IDeveloperId? DevId
    {
        get; set;
    }

    public async Task<JsonElement> GetAllProjectsAsJSONAsync()
    {
        JsonElement result = default;
        var httpManageClient = _authService.GetManagementClient(DevId);
        if (httpManageClient != null)
        {
            var projectQuery = new HttpRequestMessage(HttpMethod.Post, "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01");
            projectQuery.Content = new StringContent(Query, Encoding.UTF8, "application/json");

            // Make the request
            var response = await httpManageClient.SendAsync(projectQuery);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && content.Length > 0)
            {
                var root = JsonDocument.Parse(content).RootElement;
                root.TryGetProperty("data", out result);
            }
            else
            {
                Log.Logger()?.ReportError($"Error getting projects: {response.StatusCode} {content}");
            }
        }
        else
        {
            Log.Logger()?.ReportError($"Error getting projects: httpManageClient is null");
        }

        return result;
    }

    public async Task<JsonElement> GetBoxesAsJSONAsync(string devCenterUri, string project)
    {
        JsonElement result = default;

        // Todo: Make the client a singleton
        var httpDataClient = _authService.GetDataPlaneClient(DevId);
        if (httpDataClient != null)
        {
            var api = devCenterUri + "projects/" + project + "/users/me/devboxes?api-version=2023-07-01-preview";
            var boxQuery = new HttpRequestMessage(HttpMethod.Get, api);

            // Make the request
            var response = await httpDataClient.SendAsync(boxQuery);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && content.Length > 0)
            {
                var root = JsonDocument.Parse(content).RootElement;
                root.TryGetProperty("value", out result);
            }
            else
            {
                Log.Logger()?.ReportError($"Error getting boxes: {response.StatusCode} {content}");
            }
        }
        else
        {
            // Todo: Uncomment after testing
            // Log.Logger()?.ReportError($"Error getting boxes: httpDataClient is null");
        }

        return result;
    }
}
