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

    private ConfigurationSetState _oldSetState = ConfigurationSetState.Unknown;

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

        _fullTaskJSON = fullTask.ToString();
        _units = units;
    }

    private void SetStateForCustomizationTask(TaskJSONToCSClasses.BasePackage response)
    {
        var setState = DevBoxOperationHelper.JSONStatusToSetStatus(response.Status);

        for (var i = 0; i < _units.Count; i++)
        {
            var task = _units[i];
            var oldUnitState = task.State;
            var unitState = DevBoxOperationHelper.JSONStatusToUnitStatus(response.Tasks[i].Status);
            if (oldUnitState != unitState)
            {
                // var unitResultInformation = new ConfigurationUnitResultInformation(null, null, null, ConfigurationUnitResultSource.None);
                ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, unitState, null, task));
                ConfigurationSetStateChanged?.Invoke(this, args);
            }

            // To Do: Simplify
            _log.Debug($"-----------------------------------------------------------------> Individual Status: {unitState}");
        }

        if (_oldSetState != setState)
        {
            ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.SetStateChanged, setState, ConfigurationUnitState.Unknown, null, null));
            ConfigurationSetStateChanged?.Invoke(this, args);
            _oldSetState = setState;
        }

        _log.Debug($"-------------------------------------------------------------------------> Status: {response.Status}");
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
                    var response = JsonSerializer.Deserialize<TaskJSONToCSClasses.BasePackage>(rawResponse, _taskJsonSerializerOptions);
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
