// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using AzureExtension.DevBox.Models;
using AzureExtension.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AzureExtension.DevBox.Helpers;

public static class DevBoxOperationHelper
{
    public static string ActionToPerformToString(DevBoxActionToPerform action)
    {
        return action switch
        {
            DevBoxActionToPerform.Start => Constants.DevBoxStartOperation,
            DevBoxActionToPerform.Stop => Constants.DevBoxStopOperation,
            DevBoxActionToPerform.Restart => Constants.DevBoxRestartOperation,
            _ => string.Empty,
        };
    }

    public static DevBoxActionToPerform? StringToActionToPerform(string? actionName)
    {
        return actionName switch
        {
            Constants.DevBoxStartOperation => DevBoxActionToPerform.Start,
            Constants.DevBoxStopOperation => DevBoxActionToPerform.Stop,
            Constants.DevBoxRestartOperation => DevBoxActionToPerform.Restart,
            Constants.DevBoxRepairOperation => DevBoxActionToPerform.Repair,
            _ => null,
        };
    }

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public static (string FullJson, List<CustomizationTask> Tasks) GetFullJSONAndSubTasks(string configuration)
    {
        // Remove " dependsOn: -'Git.Git | Install: Git'" from the configuration
        configuration = configuration.Replace("dependsOn:", string.Empty);
        configuration = configuration.Replace("- 'Git.Git | Install: Git'", string.Empty);

        List<CustomizationTask> tasks = new();

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
                        tasks.Add(new CustomizationTask(resource.Directives.Description, resource, false));
                    }
                    else if (resource.Resource.EndsWith("GitClone", System.StringComparison.Ordinal))
                    {
                        tasks.Add(new CustomizationTask(resource.Directives.Description, resource, true));
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

        return (fullTask.ToString(), tasks);
    }
}
