// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

namespace DevHomeAzureExtension.DevBox.Models;

/// <summary>
/// These parameters are the parameters we'll expect to be passed in from Dev Home to create a Dev Box.
/// </summary>
public class DevBoxCreationParameters
{
    public string ProjectName { get; set; } = string.Empty;

    public string PoolName { get; set; } = string.Empty;

    public string NewEnvironmentName { get; set; } = string.Empty;

    public string DevCenterUri { get; set; } = string.Empty;

    public DevBoxCreationParameters(string projectName, string poolName, string newEnvironmentName, string devCenterUri)
    {
        ProjectName = projectName;
        PoolName = poolName;
        NewEnvironmentName = newEnvironmentName;
        DevCenterUri = devCenterUri;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Project Name: {ProjectName} ");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Pool Name: {PoolName} ");
        builder.AppendLine(CultureInfo.InvariantCulture, $"DevBox Name: {NewEnvironmentName} ");
        builder.AppendLine(CultureInfo.InvariantCulture, $"DevCenter Uri: {DevCenterUri} ");
        return builder.ToString();
    }
}
