// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using AzureExtension.Contracts;

namespace AzureExtension.DevBox.Helpers;

public class ConfigWrapper : IApplyConfigurationOperation
{
    public event TypedEventHandler<IApplyConfigurationOperation, ApplyConfigurationActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<IApplyConfigurationOperation, ConfigurationSetStateChangedEventArgs> ConfigurationSetStateChanged = (s, e) => { };

    private JsonSerializerOptions _taskJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string FullJson = string.Empty;

    public List<ConfigurationUnit> Units = new();

    public OpenConfigurationSetResult OpenConfigurationSetResult = new(null, null, null, 0, 0);

    public ApplyConfigurationSetResult ApplyConfigurationSetResult = new(null, null);

    private string configuration;

    private string taskAPI;

    private IDevBoxManagementService _managementService;

    private IDeveloperId _devId;

    public ConfigWrapper(string configuration, string taskAPI, IDevBoxManagementService devBoxManagementService, IDeveloperId associatedDeveloperId)
    {
        this.configuration = configuration;
        this.taskAPI = taskAPI;
        this._managementService = devBoxManagementService;
        this._devId = associatedDeveloperId;
    }

    public void Initialize(string fullJson, List<ConfigurationUnit> units)
    {
        FullJson = fullJson;
        Units = units;
    }

    public static ConfigWrapper GetFullJSONAndSubTasks(string configuration)
    {
        // Remove " dependsOn: -'Git.Git | Install: Git'" from the configuration
        configuration = configuration.Replace("dependsOn:", string.Empty);
        configuration = configuration.Replace("- 'Git.Git | Install: Git'", string.Empty);

        List<ConfigurationUnit> units = new();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
        var baseDSC = deserializer.Deserialize<TaskYAMLToCSClasses.BasePackage>(new StringReader(configuration));
        var resources = baseDSC?.Properties?.Resources;
        baseDSC?.Properties?.SetResources(null);
        StringBuilder fullTask = new(Constants.WingetTaskJsonBaseStart);

        if (resources != null)
        {
            foreach (var resource in resources)
            {
                if (resource.Resource is not null && resource.Directives is not null)
                {
                    if (resource.Resource.EndsWith("BasePackage", System.StringComparison.Ordinal))
                    {
                        units.Add(new("Type - Winget", resource.Directives.Description, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Inform));
                    }
                    else if (resource.Resource.EndsWith("GitClone", System.StringComparison.Ordinal))
                    {
                        units.Add(new("Type - GitClone", resource.Directives.Description, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Inform));
                    }
                }

                var tempDsc = baseDSC;
                tempDsc?.Properties?.SetResources(new List<TaskYAMLToCSClasses.ResourceItem> { resource });
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = serializer.Serialize(tempDsc);
                var encodedConfiguration = DevBoxOperationHelper.Base64Encode(yaml);
                fullTask.Append(Constants.WingetTaskJsonTaskStart + encodedConfiguration + Constants.WingetTaskJsonTaskEnd);
            }
        }

        fullTask.Remove(fullTask.Length - 1, 1);
        fullTask.Append(Constants.WingetTaskJsonBaseEnd);

        var wrapper = new ConfigWrapper();
        wrapper.Initialize(fullTask.ToString(), units);
        return wrapper;
    }


    private void SetStateForCustomizationTask(TaskJSONToCSClasses.BasePackage response)
    {
        var setState = DevBoxOperationHelper.JSONStatusToSetStatus(response.Status);

        for (var i = 0; i < _currentConfig.Units.Count; i++)
        {
            var task = _currentConfig.Units[i];
            var oldStatus = task.State;
            var newUnitStatus = DevBoxOperationHelper.JSONStatusToUnitStatus(response.Tasks[i].Status);
            if (oldStatus != newUnitStatus)
            {
                // var unitResultInformation = new ConfigurationUnitResultInformation(null, null, null, ConfigurationUnitResultSource.None);
                ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, newUnitStatus, null, task));
                ConfigurationSetStateChanged?.Invoke(this, args);
            }

            // To Do: Simplify
            _log.Debug($"-----------------------------------------------------------------> Individual Status: {newUnitStatus}");
        }
        _log.Debug($"-------------------------------------------------------------------------> Status: {response.Status}");
    }

    IAsyncOperation<ApplyConfigurationResult> IApplyConfigurationOperation.StartAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                _log.Information($"Applying config on {DisplayName} - {_currentConfig.FullJson}");

                HttpContent httpContent = new StringContent(_currentConfig.FullJson, Encoding.UTF8, "application/json");
                var result = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(_taskAPI), AssociatedDeveloperId, HttpMethod.Put, httpContent);

                var setStatus = string.Empty;
                while (setStatus != "Succeeded" && setStatus != "Failed" && setStatus != "ValidationFailed")
                {
                    await Task.Delay(setStatus == "Running" ? 2500 : 10000);

                    var poll = await _devBoxManagementService.HttpsRequestToDataPlane(new Uri(_taskAPI), AssociatedDeveloperId, HttpMethod.Get, null);
                    var rawResponse = poll.JsonResponseRoot.ToString();
                    var response = JsonSerializer.Deserialize<TaskJSONToCSClasses.BasePackage>(rawResponse, _taskJsonSerializerOptions);
                    setStatus = response?.Status;

                    if (response is not null)
                    {
                        SetStateForCustomizationTask(response);
                    }
                }

                return new ApplyConfigurationResult(_currentConfig.OpenConfigurationSetResult, _currentConfig.ApplyConfigurationSetResult);
            }
            catch (Exception ex)
            {
                UpdateStateForUI();
                _log.Error($"Unable to apply configuration {_currentConfig.FullJson}", ex);
                return new ApplyConfigurationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }

}
