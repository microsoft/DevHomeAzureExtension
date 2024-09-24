// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevHomeAzureExtension.DevBox.Helpers;

public class WingetConfigWrapper : IApplyConfigurationOperation, IDisposable
{
    // Example of the JSON payload for the customization task
    //  {
    //    "tasks": [
    //        {
    //            "name": "winget",
    //            "runAs": "User",
    //            "parameters": {
    //                "inlineConfigurationBase64": "..."
    //            },
    //        },
    //    ]
    //  }
    public const string WingetTaskJsonBaseStart = "{\"tasks\": [";

    public const string WingetTaskJsonTaskStart = @"{
            ""name"": ""winget"",
            ""runAs"": ""User"",
            ""parameters"": {
                ""inlineConfigurationBase64"": """;

    public const string WingetTaskJsonTaskEnd = "\"}},";

    public const string WingetTaskJsonBaseEnd = "]}";

    public const string ConfigApplyFailedKey = "DevBox_ConfigApplyFailedKey";

    public const string ValidationFailedKey = "DevBox_ValidationFailedKey";

    public const string DevBoxErrorStateKey = "DevBox_ErrorStateKey";

    public const string DevBoxErrorStartKey = "DevBox_ErrorStartKey";

    public event TypedEventHandler<IApplyConfigurationOperation, ApplyConfigurationActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<IApplyConfigurationOperation, ConfigurationSetStateChangedEventArgs> ConfigurationSetStateChanged = (s, e) => { };

    private readonly JsonSerializerOptions _taskJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _taskAPI;

    private readonly string _baseAPI;

    private readonly IDevBoxManagementService _managementService;

    private readonly IDeveloperId _devId;

    private readonly Serilog.ILogger _log;

    private readonly ManualResetEvent _launchEvent = new(false);

    private readonly ManualResetEvent _resumeEvent = new(false);

    // Using a common failure result for all the tasks
    // since we don't get any other information from the REST API
    private readonly ConfigurationUnitResultInformation _commonFailureResult = new(
            new WingetConfigurationException("Runtime Failure"), string.Empty, string.Empty, ConfigurationUnitResultSource.UnitProcessing);

    private string _fullTaskJSON = string.Empty;

    private List<ConfigurationUnit> _units = new();

    private OpenConfigurationSetResult _openConfigurationSetResult = new(null, null, null, 0, 0);

    private ApplyConfigurationSetResult _applyConfigurationSetResult = new(null, null);

    private string[] _oldUnitState = Array.Empty<string>();

    private bool _pendingNotificationShown;

    private bool _alreadyUpdatedUI;

    private DevBoxInstance _devBox;

    public WingetConfigWrapper(
        DevBoxInstance devBoxInstance,
        string configuration,
        IDevBoxManagementService devBoxManagementService,
        Serilog.ILogger log)
    {
        _devBox = devBoxInstance;
        _baseAPI = _devBox.DevBoxState.Uri;
        _taskAPI = $"{_baseAPI}{Constants.CustomizationAPI}{DateTime.Now.ToFileTimeUtc()}?{Constants.APIVersion}";
        _managementService = devBoxManagementService;
        _devId = _devBox.AssociatedDeveloperId;
        _log = log;
        Initialize(configuration);
    }

    public void Initialize(string configuration)
    {
        List<ConfigurationUnit> units = new();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var baseDSC = deserializer.Deserialize<TaskYAMLToCSClasses.BasePackage>(configuration);

        // Move the resources to a separate list
        var resources = baseDSC?.Properties?.Resources;

        // Remove the resources from the baseDSC
        // They will be added back, each as an individual task since we
        // cannot get individual task statuses for a single comprehensive task
        baseDSC?.Properties?.SetResources(null);

        if (resources != null)
        {
            // Start collecting the individual tasks, starting with the base
            StringBuilder fullTask = new(WingetTaskJsonBaseStart);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
                .Build();

            foreach (var resource in resources)
            {
                if (resource.Resource is not null && resource.Directives is not null)
                {
                    if (resource.Resource.Equals("Microsoft.WinGet.DSC/WinGetPackage", System.StringComparison.OrdinalIgnoreCase))
                    {
                        units.Add(new("WinGetPackage", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                    else if (resource.Resource.EndsWith("GitDsc/GitClone", System.StringComparison.OrdinalIgnoreCase))
                    {
                        units.Add(new("GitClone", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                }

                // Add the resource back as an individual task
                var tempDsc = baseDSC;

                // Remove "dependsOn:" from the configuration
                // This is a workaround as the current implementation breaks down the task into individual tasks
                // and we cannot have dependencies between them
                resource.DependsOn = null;
                tempDsc?.Properties?.SetResources(new List<TaskYAMLToCSClasses.ResourceItem> { resource });
                var yaml = serializer.Serialize(tempDsc);
                var encodedConfiguration = DevBoxOperationHelper.Base64Encode(yaml);
                fullTask.Append(WingetTaskJsonTaskStart + encodedConfiguration + WingetTaskJsonTaskEnd);
            }

            // Remove the last comma after the last task
            fullTask.Length--;
            fullTask.Append(WingetTaskJsonBaseEnd);
            _fullTaskJSON = fullTask.ToString();

            _units = units;
            _oldUnitState = Enumerable.Repeat(string.Empty, units.Count).ToArray();
        }
    }

    private void HandleEndState(TaskJSONToCSClasses.BaseClass response, bool isSetSuccessful = false)
    {
        List<ApplyConfigurationUnitResult> unitResults = new();
        for (var i = 0; i < _units.Count; i++)
        {
            var task = _units[i];
            if (isSetSuccessful || response.Tasks[i].Status == "Succeeded")
            {
                unitResults.Add(new(task, ConfigurationUnitState.Completed, false, false, null));
            }
            else
            {
                unitResults.Add(new(task, ConfigurationUnitState.Completed, false, false, _commonFailureResult));
            }
        }

        _applyConfigurationSetResult = new(null, unitResults);
    }

    private void SetStateForCustomizationTask(TaskJSONToCSClasses.BaseClass response)
    {
        var setState = DevBoxOperationHelper.JSONStatusToSetStatus(response.Status);
        _log.Information($"Set Status: {response.Status}");

        // No need to show the pending status more than once
        if (_pendingNotificationShown && setState == ConfigurationSetState.Pending)
        {
            return;
        }

        switch (response.Status)
        {
            case "NotStarted":
                ConfigurationSetStateChanged?.Invoke(this, new(new(ConfigurationSetChangeEventType.SetStateChanged, setState, ConfigurationUnitState.Unknown, null, null)));
                _pendingNotificationShown = true;
                break;

            case "Running":
                var isAnyTaskRunning = false;
                var isWaitingForUserSession = false;
                for (var i = 0; i < _units.Count; i++)
                {
                    var responseStatus = response.Tasks[i].Status;
                    _log.Information($"Unit Response Status: {responseStatus}");

                    // If the status is waiting for a user session, there is no need to check for other
                    // individual task statuses.
                    if (responseStatus == "WaitingForUserSession")
                    {
                        isWaitingForUserSession = true;
                        continue;
                    }

                    if (responseStatus == "Running")
                    {
                        isAnyTaskRunning = true;
                    }

                    if (_oldUnitState[i] != responseStatus)
                    {
                        var task = _units[i];
                        if (responseStatus == "Failed")
                        {
                            // Wait for a few seconds before fetching the logs
                            // otherwise the logs might not be available
                            Thread.Sleep(TimeSpan.FromSeconds(15));

                            // Make the log API string : Remove the API version from the URI and add 'logs'
                            var logURI = _taskAPI.Substring(0, _taskAPI.LastIndexOf('?'));
                            var id = response.Tasks[i].Id;
                            logURI += $"/logs/{id}?{Constants.APIVersion}";

                            // Fetch the logs
                            var log = _managementService.HttpsRequestToDataPlaneRawResponse(new Uri(logURI), _devId, HttpMethod.Get).GetAwaiter().GetResult();

                            // Split the log into lines
                            var logLines = log.Split('\n');

                            // Find index of line with "Result" in it
                            // The error message is in the next line
                            var errorIndex = Array.FindIndex(logLines, x => x.Contains("Result")) + 1;
                            var errorMessage = errorIndex >= 0 ? logLines[errorIndex] : Resources.GetResource(Constants.DevBoxCheckLogsKey);

                            // Make the result info to show in the UI
                            var resultInfo = new ConfigurationUnitResultInformation(
                                new WingetConfigurationException("Runtime Failure"), errorMessage, string.Empty, ConfigurationUnitResultSource.UnitProcessing);
                            ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, ConfigurationUnitState.Completed, resultInfo, task));
                            ConfigurationSetStateChanged?.Invoke(this, args);
                        }
                        else
                        {
                            var unitState = DevBoxOperationHelper.JSONStatusToUnitStatus(responseStatus);
                            ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, unitState, null, task));
                            ConfigurationSetStateChanged?.Invoke(this, args);
                        }

                        _oldUnitState[i] = responseStatus;
                    }
                }

                // If waiting for user session and no task is running, show the adaptive card
                // We add a wait to give Dev Box time to start the task
                // But if this is the first login, Dev Box Agent needs to configure itself
                // So an extra wait might be needed.
                if (isWaitingForUserSession && !isAnyTaskRunning)
                {
                    if (_alreadyUpdatedUI)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(100));
                        _alreadyUpdatedUI = false;
                    }
                    else
                    {
                        ApplyConfigurationActionRequiredEventArgs eventArgs = new(new WaitingForUserAdaptiveCardSession(_resumeEvent, _launchEvent));
                        ActionRequired?.Invoke(this, eventArgs);
                        WaitHandle.WaitAny(new[] { _resumeEvent });

                        // Check if the launch event is also set
                        // If it is, wait for a few seconds longer to account for it
                        if (_launchEvent.WaitOne(0))
                        {
                            _log.Information("Launching the dev box");
                            _devBox.ConnectAsync(string.Empty).GetAwaiter().GetResult();
                            Thread.Sleep(TimeSpan.FromSeconds(30));
                            _launchEvent.Reset();
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(20));
                        _alreadyUpdatedUI = true;
                    }

                    _resumeEvent.Reset();
                }

                break;

            case "ValidationFailed":
                _openConfigurationSetResult = new(new FormatException(Resources.GetResource(ValidationFailedKey)), null, null, 0, 0);
                break;

            case "Succeeded":
                HandleEndState(response);
                break;

            case "Failed":
                HandleEndState(response);
                break;
        }
    }

    // Start the Dev Box if it isn't already running
    private async Task HandleNonRunningState()
    {
        // Check if the dev box might have been started in the meantime
        if (_devBox.GetState() == ComputeSystemState.Running)
        {
            return;
        }

        // Start the Dev Box
        try
        {
            _log.Information("Starting the dev box to apply configuration");
            var startResult = await _devBox.StartAsync(string.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unable to start the dev box");
            throw new InvalidOperationException(Resources.GetResource(DevBoxErrorStateKey, _devBox.DisplayName));
        }

        // Notify the user
        ConfigurationSetStateChanged?.Invoke(this, new(new(ConfigurationSetChangeEventType.SetStateChanged, ConfigurationSetState.StartingDevice, ConfigurationUnitState.Unknown, null, null)));

        // Wait for the Dev Box to start with a timeout of 15 minutes
        var delay = TimeSpan.FromSeconds(30);
        var waitTimeLeft = TimeSpan.FromMinutes(15);

        // Wait for the Dev Box to start, polling every 30 seconds
        while ((waitTimeLeft > TimeSpan.Zero) && (_devBox.GetState() != ComputeSystemState.Running))
        {
            await Task.Delay(delay);
            waitTimeLeft -= delay;
        }

        // If there was a timeout
        if (_devBox.GetState() != ComputeSystemState.Running)
        {
            throw new InvalidOperationException(Resources.GetResource(DevBoxErrorStateKey, _devBox.DisplayName));
        }

        var timeTaken = TimeSpan.FromMinutes(15) - waitTimeLeft;
        _log.Information($"Started successfully after {timeTaken.Minutes} minutes");
    }

    IAsyncOperation<ApplyConfigurationResult> IApplyConfigurationOperation.StartAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                if (_devBox.GetState() != ComputeSystemState.Running)
                {
                    await HandleNonRunningState();
                }

                _log.Information($"Applying config {_fullTaskJSON}");

                HttpContent httpContent = new StringContent(_fullTaskJSON, Encoding.UTF8, "application/json");
                var result = await _managementService.HttpsRequestToDataPlane(new Uri(_taskAPI), _devId, HttpMethod.Put, httpContent);

                var setStatus = string.Empty;
                while (setStatus != "Succeeded" && setStatus != "Failed" && setStatus != "ValidationFailed")
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));
                    var poll = await _managementService.HttpsRequestToDataPlane(new Uri(_taskAPI), _devId, HttpMethod.Get, null);
                    var rawResponse = poll.JsonResponseRoot.ToString();
                    var response = JsonSerializer.Deserialize<TaskJSONToCSClasses.BaseClass>(rawResponse, _taskJsonSerializerOptions);
                    setStatus = response?.Status;

                    if (response is not null)
                    {
                        SetStateForCustomizationTask(response);
                    }
                }

                return new ApplyConfigurationResult(_openConfigurationSetResult, _applyConfigurationSetResult);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unable to apply configuration {_fullTaskJSON}");
                return new ApplyConfigurationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }

    void IDisposable.Dispose()
    {
        _resumeEvent?.Dispose();
        GC.SuppressFinalize(this);
    }
}
