// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text;
using System.Text.Json;
using AzureExtension.Contracts;
using DevHomeAzureExtension.DataModel;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class ManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    public ManagementService(IDevBoxAuthService authService) => _authService = authService;

    private static readonly string Query =
        "{\"query\": \"Resources | where type in~ ('microsoft.devcenter/projects') | where properties['provisioningState'] =~ 'Succeeded' | project id, location, tenantId, name, properties, type\"," +
        " \"options\":{\"allowPartialScopes\":true}}";

    public IDeveloperId? DevId
    {
        get; set;
    }

    public async Task<JsonElement> GetAllProjectsAsJsonAsync()
    {
        JsonElement result = default;
        var httpManageClient = _authService.GetManagementClient(DevId);
        try
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
                Log.Logger()?.ReportError($"GetAllProjectsAsJsonAsync: {response.StatusCode} {content}");
            }
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"GetAllProjectsAsJsonAsync: Exception {e.Message}");
        }

        return result;
    }

    public async Task<JsonElement> GetBoxesAsJsonAsync(string devCenterUri, string project)
    {
        JsonElement result = default;
        var httpDataClient = _authService.GetDataPlaneClient(DevId);
        try
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
                Log.Logger()?.ReportError($"GetBoxesAsJsonAsync: {response.StatusCode} {content}");
            }
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"GetBoxesAsJsonAsync: Exception: {e.Message}");
        }

        return result;
    }
}
