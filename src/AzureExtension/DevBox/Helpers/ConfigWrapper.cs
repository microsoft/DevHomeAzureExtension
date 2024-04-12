// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using AzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AzureExtension.DevBox.Helpers;

public class ConfigWrapper : IApplyConfigurationOperation
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
            ""name"": ""Quickstart-Catalog-Tasks/winget"",
			""runAs"": ""User"",
            ""parameters"": {
                ""inlineConfigurationBase64"": """;

    public const string WingetTaskJsonTaskEnd = "\"}},";

    public const string WingetTaskJsonBaseEnd = "]}";

    public const string ConfigApplyFailedKey = "DevBox_ConfigApplyFailedKey";

    public const string ValidationFailedKey = "DevBox_ValidationFailedKey";

    public event TypedEventHandler<IApplyConfigurationOperation, ApplyConfigurationActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<IApplyConfigurationOperation, ConfigurationSetStateChangedEventArgs> ConfigurationSetStateChanged = (s, e) => { };

    private JsonSerializerOptions _taskJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private string _fullTaskJSON = string.Empty;

    private List<ConfigurationUnit> _units = new();

    private OpenConfigurationSetResult _openConfigurationSetResult = new(null, null, null, 0, 0);

    private ApplyConfigurationSetResult _applyConfigurationSetResult = new(null, null);

    private string _restAPI;

    private IDevBoxManagementService _managementService;

    private IDeveloperId _devId;

    private Serilog.ILogger _log;

    private ConfigurationUnitState[] _oldUnitState = Array.Empty<ConfigurationUnitState>();

    private bool _pendingNotificationShown;

    public ConfigWrapper(string configuration, string taskAPI, IDevBoxManagementService devBoxManagementService, IDeveloperId associatedDeveloperId, Serilog.ILogger log)
    {
        _restAPI = taskAPI;
        _managementService = devBoxManagementService;
        _devId = associatedDeveloperId;
        _log = log;
        Initialize(configuration);
    }

    public void Initialize(string configuration)
    {
        List<ConfigurationUnit> units = new();

        // Remove " dependsOn: -'Git.Git | Install: Git'" from the configuration
        // This is a workaround as the current implementation does not support dependsOn
        configuration = configuration.Replace("dependsOn:", string.Empty);
        configuration = configuration.Replace("- 'Git.Git | Install: Git'", string.Empty);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
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
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections)
                .Build();

            foreach (var resource in resources)
            {
                if (resource.Resource is not null && resource.Directives is not null)
                {
                    if (resource.Resource.EndsWith("WinGetPackage", System.StringComparison.Ordinal))
                    {
                        units.Add(new("WinGetPackage", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                    else if (resource.Resource.EndsWith("GitClone", System.StringComparison.Ordinal))
                    {
                        units.Add(new("GitClone", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                }

                // Add the resource back as an individual task
                var tempDsc = baseDSC;
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
            _oldUnitState = Enumerable.Repeat(ConfigurationUnitState.Unknown, units.Count).ToArray();
        }
    }

    private void SetStateForCustomizationTask(TaskJSONToCSClasses.BaseClass response)
    {
        var setState = DevBoxOperationHelper.JSONStatusToSetStatus(response.Status);
        _log.Debug($"Set Status: {response.Status}");

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
                for (var i = 0; i < _units.Count; i++)
                {
                    var task = _units[i];
                    var unitState = DevBoxOperationHelper.JSONStatusToUnitStatus(response.Tasks[i].Status);
                    if (_oldUnitState[i] != unitState)
                    {
                        ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, unitState, null, task));
                        ConfigurationSetStateChanged?.Invoke(this, args);
                        _oldUnitState[i] = unitState;
                    }

                    _log.Debug($"Unit Status: {unitState}");
                }

                break;

            case "Succeeded":
                List<ApplyConfigurationUnitResult> unitResults = new();
                for (var i = 0; i < _units.Count; i++)
                {
                    var task = _units[i];
                    unitResults.Add(new(task, ConfigurationUnitState.Completed, false, false, null));
                }

                _applyConfigurationSetResult = new(null, unitResults);
                break;

            case "ValidationFailed":
                _openConfigurationSetResult = new(new FormatException(Resources.GetResource(ValidationFailedKey)), null, null, 0, 0);
                break;

            case "Failed":
                // To Do: Add case. Currently the API only checks if Winget started the task.
                // Does not check if the task failed.
                // Send UnitStateChanged event for the failed task with ResultInfo and ResultSource as UnitProcessing
                // _applyConfigurationSetResult = new(new ApplicationException(Resources.GetResource(ConfigApplyFailedKey)), null);
                break;
        }
    }

    IAsyncOperation<ApplyConfigurationResult> IApplyConfigurationOperation.StartAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                _log.Information($"Applying config {_fullTaskJSON}");

                HttpContent httpContent = new StringContent(_fullTaskJSON, Encoding.UTF8, "application/json");
                var result = await _managementService.HttpsRequestToDataPlane(new Uri(_restAPI), _devId, HttpMethod.Put, httpContent);

                var setStatus = string.Empty;
                while (setStatus != "Succeeded" && setStatus != "Failed" && setStatus != "ValidationFailed")
                {
                    await Task.Delay(setStatus == "Running" ? 2500 : 10000);

                    var poll = await _managementService.HttpsRequestToDataPlane(new Uri(_restAPI), _devId, HttpMethod.Get, null);
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
                _log.Error($"Unable to apply configuration {_fullTaskJSON}", ex);
                return new ApplyConfigurationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }
}
