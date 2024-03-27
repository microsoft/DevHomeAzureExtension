// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Exceptions;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace AzureExtension.Services.DevBox;

/// <summary>
/// The DevBoxManagementService is responsible for making calls to the Azure Resource Graph API.
/// All calls to the Azure Resource Graph API should be made through this service.
/// </summary>
public class DevBoxManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevBoxManagementService));

    public DevBoxManagementService(IDevBoxAuthService authService) => _authService = authService;

    private const string DevBoxManagementServiceName = nameof(DevBoxManagementService);

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToManagementPlane"/>/>
    public async Task<DevBoxHttpsRequestResult> HttpsRequestToManagementPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpManagementClient = _authService.GetManagementClient(developerId);
        return await DevBoxHttpRequest(httpManagementClient, webUri, developerId, method, requestContent);
    }

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToDataPlane"/>
    public async Task<DevBoxHttpsRequestResult> HttpsRequestToDataPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpDataClient = _authService.GetDataPlaneClient(developerId);
        return await DevBoxHttpRequest(httpDataClient, webUri, developerId, method, requestContent);
    }

    private async Task<DevBoxHttpsRequestResult> DevBoxHttpRequest(HttpClient client, Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        try
        {
            var devBoxQuery = new HttpRequestMessage(method, webUri);
            devBoxQuery.Content = requestContent;

            // Make the request
            var response = await client.SendAsync(devBoxQuery);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && content.Length > 0)
            {
                return new(JsonDocument.Parse(content).RootElement, new DevBoxOperationResponseHeader(response.Headers));
            }

            throw new HttpRequestException($"DevBoxHttpRequest failed: {response.StatusCode} {content}");
        }
        catch (Exception ex)
        {
            _log.Error($"DevBoxHttpRequest failed: Exception", ex);
            throw;
        }
    }

    /// <inheritdoc cref="IDevBoxManagementService.GetAllProjectsToPoolsMappingAsync"/>
    public async Task<List<DevBoxProjectAndPoolContainer>> GetAllProjectsToPoolsMappingAsync(JsonElement projectsJson, IDeveloperId developerId)
    {
        var projectsToPoolsMapping = new List<DevBoxProjectAndPoolContainer>();

        foreach (var projectJson in projectsJson.EnumerateArray())
        {
            try
            {
                var projectObj = JsonSerializer.Deserialize<DevBoxProject>(projectJson.ToString(), Constants.JsonOptions)!;
                var properties = projectObj.Properties;
                var uriToRetrievePools = $"{properties.DevCenterUri}{Constants.Projects}/{projectObj.Name}/{Constants.Pools}?{Constants.APIVersion}";
                var result = await HttpsRequestToDataPlane(new Uri(uriToRetrievePools), developerId, HttpMethod.Get);
                var pools = JsonSerializer.Deserialize<DevBoxPoolRoot>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
                var container = new DevBoxProjectAndPoolContainer { Project = projectObj, Pools = pools };

                projectsToPoolsMapping.Add(container);
            }
            catch (Exception ex)
            {
                projectJson.TryGetProperty("name", out var projectWithErrorName);
                _log.Error($"unable to get pools for {projectWithErrorName}", ex);
            }
        }

        return projectsToPoolsMapping;
    }

    /// <inheritdoc cref="IDevBoxManagementService.CreateDevBox"/>
    public async Task<DevBoxHttpsRequestResult> CreateDevBox(DevBoxCreationParameters parameters, IDeveloperId developerId)
    {
        if (!Regex.IsMatch(parameters.DevBoxName, Constants.NameRegexPattern))
        {
            throw new DevBoxNameInvalidException($"Unable to create Dev Box due to Invalid Dev Box name: {parameters.DevBoxName}");
        }

        if (!Regex.IsMatch(parameters.ProjectName, Constants.NameRegexPattern))
        {
            throw new DevBoxProjectNameInvalidException($"Unable to create Dev Box due to Invalid project name: {parameters.ProjectName}");
        }

        var uriToCreateDevBox = $"{parameters.DevCenterUri}{Constants.Projects}/{parameters.ProjectName}{Constants.DevBoxUserSegmentOfUri}/{parameters.DevBoxName}?{Constants.APIVersion}";
        var contentJson = JsonSerializer.Serialize(new DevBoxCreationPoolName(parameters.PoolName));
        var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
        return await HttpsRequestToDataPlane(new Uri(uriToCreateDevBox), developerId, HttpMethod.Put, content);
    }
}
