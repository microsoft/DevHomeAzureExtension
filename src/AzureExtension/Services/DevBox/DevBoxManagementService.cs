// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.DevBox.Helpers;
using DevHomeAzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using DevBoxConstants = DevHomeAzureExtension.DevBox.Constants;

namespace DevHomeAzureExtension.Services.DevBox;

/// <summary>
/// The DevBoxManagementService is responsible for making calls to the Azure Resource Graph API.
/// All calls to the Azure Resource Graph API should be made through this service.
/// </summary>
public class DevBoxManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevBoxManagementService));

    private readonly Dictionary<string, List<DevBoxProjectAndPoolContainer>> _projectAndPoolContainerMap = new();

    public DevBoxManagementService(IDevBoxAuthService authService) => _authService = authService;

    private const string DevBoxManagementServiceName = nameof(DevBoxManagementService);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string _writeAbility = "WriteDevBoxes";

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToManagementPlane"/>/>
    public async Task<DevBoxHttpsRequestResult> HttpsRequestToManagementPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpManagementClient = _authService.GetManagementClient(developerId);
        var result = await DevBoxHttpRequest(httpManagementClient, webUri, developerId, method, requestContent);
        try
        {
            return new(JsonDocument.Parse(result.Content).RootElement, new DevBoxOperationResponseHeader(result.ResponseHeaders));
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"HttpsRequestToManagementPlane failed: Exception");
            throw;
        }
    }

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToDataPlane"/>
    public async Task<DevBoxHttpsRequestResult> HttpsRequestToDataPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpDataClient = _authService.GetDataPlaneClient(developerId);
        var result = await DevBoxHttpRequest(httpDataClient, webUri, developerId, method, requestContent);
        try
        {
            return new(JsonDocument.Parse(result.Content).RootElement, new DevBoxOperationResponseHeader(result.ResponseHeaders));
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"HttpsRequestToDataPlane failed: Exception");
            throw;
        }
    }

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToDataPlane"/>
    public async Task<string> HttpsRequestToDataPlaneRawResponse(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpDataClient = _authService.GetDataPlaneClient(developerId);
        var result = await DevBoxHttpRequest(httpDataClient, webUri, developerId, method, requestContent);
        return result.Content;
    }

    private async Task<(string Content, HttpResponseHeaders ResponseHeaders)> DevBoxHttpRequest(HttpClient client, Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
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
                return (content, response.Headers);
            }

            throw new HttpRequestException($"DevBoxHttpRequest failed: {response.StatusCode} {content}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"DevBoxHttpRequest failed: Exception");
            throw;
        }
    }

    public async Task<string> HttpsRequestToDataPlaneWithRawResponse(HttpClient client, Uri webUri, IDeveloperId developerId, HttpMethod method)
    {
        try
        {
            var devBoxQuery = new HttpRequestMessage(method, webUri);

            // Make the request
            var response = await client.SendAsync(devBoxQuery);
            var content = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && content.Length > 0)
            {
                return content;
            }

            throw new HttpRequestException($"DevBoxRawRequest failed: {response.StatusCode} {content}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"DevBoxRawRequest failed: Exception");
            throw;
        }
    }

    // Filter out projects that the user does not have write access to
    private bool DeveloperHasWriteAbility(DevBoxProject project, IDeveloperId developerId)
    {
        try
        {
            var uri = $"{project.Properties.DevCenterUri}{DevBoxConstants.Projects}/{project.Name}/users/me/abilities?{DevBoxConstants.APIVersion}";
            var result = HttpsRequestToDataPlane(new Uri(uri), developerId, HttpMethod.Get, null).Result;
            var rawResponse = result.JsonResponseRoot.ToString();
            var abilities = JsonSerializer.Deserialize<AbilitiesJSONToCSClasses.BaseClass>(rawResponse, _jsonOptions);
            _log.Debug($"Response from abilities: {rawResponse}");

            if (abilities!.AbilitiesAsDeveloper.Contains(_writeAbility) || abilities!.AbilitiesAsAdmin.Contains(_writeAbility))
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Unable to get abilities for {project.Name}");
            return true;
        }
    }

    /// <inheritdoc cref="IDevBoxManagementService.GetAllProjectsToPoolsMappingAsync"/>
    public async Task<List<DevBoxProjectAndPoolContainer>> GetAllProjectsToPoolsMappingAsync(DevBoxProjects projects, IDeveloperId developerId)
    {
        var uniqueUserId = $"{developerId.LoginId}#{developerId.Url}";

        if (_projectAndPoolContainerMap.TryGetValue(uniqueUserId, out var devBoxProjectAndPools))
        {
            return devBoxProjectAndPools;
        }

        var projectsToPoolsMapping = new ConcurrentBag<DevBoxProjectAndPoolContainer>();

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(2));
        var token = cancellationTokenSource.Token;

        await Parallel.ForEachAsync(projects.Data!, async (project, token) =>
        {
            if (!DeveloperHasWriteAbility(project, developerId))
            {
                return;
            }

            try
            {
                var properties = project.Properties;
                var uriToRetrievePools = $"{properties.DevCenterUri}{DevBoxConstants.Projects}/{project.Name}/{DevBoxConstants.Pools}?{DevBoxConstants.APIVersion}";
                var result = await HttpsRequestToDataPlane(new Uri(uriToRetrievePools), developerId, HttpMethod.Get);
                var pools = JsonSerializer.Deserialize<DevBoxPoolRoot>(result.JsonResponseRoot.ToString(), DevBoxConstants.JsonOptions);

                // Sort the pools by name, case insensitive
                if (pools?.Value != null)
                {
                    pools.Value = new(pools.Value.OrderBy(x => x.Name));
                }

                var container = new DevBoxProjectAndPoolContainer { Project = project, Pools = pools };
                projectsToPoolsMapping.Add(container);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"unable to get pools for {project.Name}");
            }
        });

        // Sort the mapping by project name, case insensitive
        projectsToPoolsMapping = new(projectsToPoolsMapping.OrderByDescending(x => x.Project?.Name));

        _projectAndPoolContainerMap.Add(uniqueUserId, projectsToPoolsMapping.ToList());
        return projectsToPoolsMapping.ToList();
    }

    /// <inheritdoc cref="IDevBoxManagementService.CreateDevBox"/>
    public async Task<DevBoxHttpsRequestResult> CreateDevBox(DevBoxCreationParameters parameters, IDeveloperId developerId)
    {
        if (!Regex.IsMatch(parameters.NewEnvironmentName, DevBoxConstants.NameRegexPattern))
        {
            throw new DevBoxNameInvalidException($"Unable to create Dev Box due to Invalid Dev Box name: {parameters.NewEnvironmentName}");
        }

        if (!Regex.IsMatch(parameters.ProjectName, DevBoxConstants.NameRegexPattern))
        {
            throw new DevBoxProjectNameInvalidException($"Unable to create Dev Box due to Invalid project name: {parameters.ProjectName}");
        }

        var uriToCreateDevBox = $"{parameters.DevCenterUri}{DevBoxConstants.Projects}/{parameters.ProjectName}{DevBoxConstants.DevBoxUserSegmentOfUri}/{parameters.NewEnvironmentName}?{DevBoxConstants.APIVersion}";
        var contentJson = JsonSerializer.Serialize(new DevBoxCreationPoolName(parameters.PoolName));
        var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
        return await HttpsRequestToDataPlane(new Uri(uriToCreateDevBox), developerId, HttpMethod.Put, content);
    }
}
