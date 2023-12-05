// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text;
using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

/// <summary>
/// The DevBoxManagementService is responsible for making calls to the Azure Resource Graph API
/// to get the list of projects and then to the DevCenter API to get the list of DevBoxes for each project.
/// </summary>
public class DevBoxManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    public DevBoxManagementService(IDevBoxAuthService authService) => _authService = authService;

    public IDeveloperId? DevId
    {
        get; set;
    }

    /// <summary>
    /// Get the list of projects from the Azure Resource Graph API.
    /// that a user has access to.
    /// </summary>
    public async Task<JsonElement> GetAllProjectsAsJsonAsync()
    {
        JsonElement result = default;
        var httpManageClient = _authService.GetManagementClient(DevId);
        try
        {
            var projectQuery = new HttpRequestMessage(HttpMethod.Post, Constants.ARGQueryAPI);
            projectQuery.Content = new StringContent(Constants.ARGQuery, Encoding.UTF8, "application/json");

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
            Log.Logger()?.ReportError($"GetAllProjectsAsJsonAsync: Exception {e.ToString}");
        }

        return result;
    }

    /// <summary>
    /// Get the list of DevBoxes for a given project from the DevCenter API.
    /// </summary>
    public async Task<JsonElement> GetDevBoxesAsJsonAsync(string devCenterUri, string project)
    {
        JsonElement result = default;
        var httpDataClient = _authService.GetDataPlaneClient(DevId);
        try
        {
            var api = devCenterUri + "projects/" + project + Constants.DevBoxAPI;
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
            Log.Logger()?.ReportError($"GetBoxesAsJsonAsync: Exception: {e.ToString}");
        }

        return result;
    }
}
