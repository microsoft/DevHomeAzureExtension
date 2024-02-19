// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Exceptions;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;
using Windows.System.Threading;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

public enum ProvisioningStatus
{
    Succeeded,
    Failed,
}

/// <summary>
/// The DevBoxManagementService is responsible for making calls to the Azure Resource Graph API
/// to get the list of projects and then to the DevCenter API to get the list of DevBoxes for each project.
/// </summary>
public class DevBoxManagementService : IDevBoxManagementService
{
    private readonly IDevBoxAuthService _authService;

    public DevBoxManagementService(IDevBoxAuthService authService) => _authService = authService;

    private const string DevBoxManagementServiceName = nameof(DevBoxManagementService);

    private readonly Dictionary<string, KeyValuePair<ThreadPoolTimer, DateTime>> _timers = new();

    private readonly TimeSpan _operationDeadLine = TimeSpan.FromHours(2);

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToManagementPlane"/>/>
    public async Task<DevBoxOperationResult> HttpRequestToManagementPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpManagementClient = _authService.GetManagementClient(developerId);
        return await DevBoxHttpRequest(httpManagementClient, webUri, developerId, method, requestContent);
    }

    /// <inheritdoc cref="IDevBoxManagementService.HttpRequestToDataPlane"/>
    public async Task<DevBoxOperationResult> HttpRequestToDataPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
    {
        var httpDataClient = _authService.GetDataPlaneClient(developerId);
        return await DevBoxHttpRequest(httpDataClient, webUri, developerId, method, requestContent);
    }

    private async Task<DevBoxOperationResult> DevBoxHttpRequest(HttpClient client, Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null)
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

            throw new HttpRequestException($"HttpGetRequestToDataPlane failed: {response.StatusCode} {content}");
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError(DevBoxManagementServiceName, $"HttpGetRequestToDataPlane failed: Exception", ex);
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
                var result = await HttpRequestToDataPlane(new Uri(uriToRetrievePools), developerId, HttpMethod.Get);
                var pools = JsonSerializer.Deserialize<DevBoxPoolRoot>(result.JsonResponseRoot.ToString(), Constants.JsonOptions);
                var container = new DevBoxProjectAndPoolContainer { Project = projectObj, Pools = pools };

                projectsToPoolsMapping.Add(container);
            }
            catch (Exception ex)
            {
                projectJson.TryGetProperty("name", out var projectWithErrorName);
                Log.Logger()?.ReportError(DevBoxManagementServiceName, $"unable to get pools for {projectWithErrorName}", ex);
            }
        }

        return projectsToPoolsMapping;
    }

    /// <inheritdoc cref="IDevBoxManagementService.CreateDevBox"/>
    public async Task<DevBoxOperationResult> CreateDevBox(DevBoxCreationParameters parameters, IDeveloperId developerId)
    {
        if (Regex.IsMatch(parameters.DevBoxName, Constants.NameRegexPattern) == false)
        {
            throw new DevBoxNameInvalidException($"Unable to create Dev Box due to Invalid Dev Box name: {parameters.DevBoxName}");
        }

        if (Regex.IsMatch(parameters.ProjectName, Constants.NameRegexPattern) == false)
        {
            throw new DevBoxProjectNameInvalidException($"Unable to create Dev Box due to Invalid project name: {parameters.ProjectName}");
        }

        var uriToCreateDevBox = $"{parameters.DevCenterUri}{Constants.Projects}/{parameters.ProjectName}{Constants.DevBoxUserSegmentOfUri}/{parameters.DevBoxName}?{Constants.APIVersion}";
        var contentJson = JsonSerializer.Serialize(new { parameters.PoolName });
        var content = new StringContent(contentJson, Encoding.UTF8, "application/json");
        return await HttpRequestToDataPlane(new Uri(uriToCreateDevBox), developerId, HttpMethod.Put, content);
    }

    /// <inheritdoc cref="IDevBoxManagementService.StartDevCenterOperationMonitor"/>"
    public void StartDevCenterOperationMonitor(IDeveloperId developerId, DevBoxOperationResponseHeader originalHeader, TimeSpan timerInterval, Action<DevBoxLongRunningOperation> callback)
    {
        var newTimer = ThreadPoolTimer.CreatePeriodicTimer(
            async (ThreadPoolTimer timer) =>
            {
                try
                {
                    // Query the Dev Center for the status of the Dev Box operation.
                    var result = await HttpRequestToDataPlane(originalHeader.OperationLocation!, developerId, HttpMethod.Get);
                    var longRunningOperation = JsonSerializer.Deserialize<DevBoxLongRunningOperation>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;
                    callback(longRunningOperation);
                    switch (longRunningOperation.Status)
                    {
                        case Constants.DevBoxOperationNotStartedState:
                        case Constants.DevBoxRunningState:
                            break;
                        default:
                            Log.Logger()?.ReportInfo(DevBoxManagementServiceName, $"Dev Box operation monitoring stopped. {longRunningOperation}");
                            RemoveTimer(originalHeader.Id);
                            break;
                    }

                    ThrowIfTimerPastDeadline(originalHeader.Id);
                }
                catch (Exception ex)
                {
                    Log.Logger()?.ReportError(DevBoxManagementServiceName, $"Dev Box operation monitoring stopped. {ex}");
                    callback(DevBoxLongRunningOperation.CreateFailureOperation());
                    RemoveTimer(originalHeader.Id);
                }
            },
            timerInterval);

        _timers.Add(originalHeader.Id, new(newTimer, DateTime.Now));
    }

    /// See <inheritdoc cref="IDevBoxManagementService.StartDevBoxProvisioningStatusMonitor(IDeveloperId, TimeSpan, DevBoxInstance, Action{DevBoxInstance})"/>
    public void StartDevBoxProvisioningStatusMonitor(IDeveloperId developerId, TimeSpan timerInterval, DevBoxInstance devBoxInstance, Action<DevBoxInstance> callback)
    {
        var newTimer = ThreadPoolTimer.CreatePeriodicTimer(
            async (ThreadPoolTimer timer) =>
            {
                try
                {
                    // Query the Dev Center for the provisioning status of the Dev Box. This is needed for when the Dev Box was created outside of Dev Home.
                    var devBoxUri = $"{devBoxInstance.DevBoxState.Uri}?{Constants.APIVersion}";
                    var result = await HttpRequestToDataPlane(new Uri(devBoxUri), developerId, HttpMethod.Get);
                    var devBoxState = JsonSerializer.Deserialize<DevBoxMachineState>(result.JsonResponseRoot.ToString(), Constants.JsonOptions)!;

                    // If the Dev Box is no longer being created, update the state for Dev Homes UI and end the timer.
                    if (!(devBoxState.ProvisioningState == Constants.DevBoxCreatingProvisioningState || devBoxState.ProvisioningState == Constants.DevBoxProvisioningState))
                    {
                        Log.Logger()?.ReportInfo(DevBoxManagementServiceName, $"Dev Box provisioning now completed.");
                        devBoxInstance.ProvisioningMonitorCompleted(devBoxState, ProvisioningStatus.Succeeded);
                        callback(devBoxInstance);
                        RemoveTimer(devBoxInstance.Id);
                    }

                    ThrowIfTimerPastDeadline(devBoxInstance.Id);
                }
                catch (Exception ex)
                {
                    Log.Logger()?.ReportError(DevBoxManagementServiceName, $"Dev Box provisioning monitoring stopped unexpectedly. {ex}");
                    devBoxInstance.ProvisioningMonitorCompleted(null, ProvisioningStatus.Failed);
                    callback(devBoxInstance);
                    RemoveTimer(devBoxInstance.Id);
                }
            },
            timerInterval);

        _timers.Add(devBoxInstance.Id, new(newTimer, DateTime.Now));
    }

    private void ThrowIfTimerPastDeadline(string id)
    {
        if (_timers.TryGetValue(id, out var timerKeyPair))
        {
            if (DateTime.Now - timerKeyPair.Value > _operationDeadLine)
            {
                throw new DevBoxOperationMonitorException($"The operation has exceeded its '{_operationDeadLine.TotalHours} hour' deadline");
            }
        }
    }

    private void RemoveTimer(string id)
    {
        if (_timers.TryGetValue(id, out var timerKeyPair))
        {
            timerKeyPair.Key.Cancel();
            _timers.Remove(id);
        }
    }
}
