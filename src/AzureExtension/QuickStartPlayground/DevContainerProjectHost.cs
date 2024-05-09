// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.QuickStartPlayground;

public sealed class DevContainerProjectHost : IQuickStartProjectHost
{
    public DevContainerProjectHost(string displayName, string projectToLaunch)
    {
        DisplayName = displayName;
        ProjectToLaunch = projectToLaunch;
        Icon = null!;
    }

    public string DisplayName { get; private set; }

    public Uri Icon { get; private set; }

    public string ProjectToLaunch { get; private set; }

    public ProviderOperationResult Launch()
    {
        try
        {
            if (string.Equals(DisplayName, "Visual Studio Code", StringComparison.Ordinal))
            {
                LaunchVSCodeForProject();
            }
            else if (string.Equals(DisplayName, "Visual Studio Code Insiders", StringComparison.Ordinal))
            {
                LaunchVSCodeForProject(true);
            }
            else
            {
                throw new NotImplementedException($"Launching {DisplayName} is not supported.");
            }

            return new ProviderOperationResult(ProviderOperationStatus.Success, null, string.Empty, string.Empty);
        }
        catch (Exception e)
        {
            return new ProviderOperationResult(ProviderOperationStatus.Failure, e, Resources.GetResource(@"QuickstartPlayground_FailedToLaunch", DisplayName), e.Message);
        }
    }

    private void LaunchVSCodeForProject(bool insider = false)
    {
        var launchScript = "code.cmd";
        if (insider)
        {
            launchScript = "code-insiders.cmd";
        }

        using var vsCode = new Process();
        vsCode.StartInfo.UseShellExecute = false;
        vsCode.StartInfo.CreateNoWindow = true;
        vsCode.StartInfo.FileName = InstalledAppsService.FindExecutableInPath(launchScript) ?? throw new FileNotFoundException($"Couldn't locate {launchScript}");
        vsCode.StartInfo.Arguments = ProjectToLaunch;
        vsCode.Start();
    }

    public static IQuickStartProjectHost[] GetProjectHosts(string projectToLaunch, bool isVSCodeInsidersInstalled)
    {
        if (isVSCodeInsidersInstalled)
        {
            return
            [
                new DevContainerProjectHost("Visual Studio Code", projectToLaunch),
                new DevContainerProjectHost("Visual Studio Code Insiders", projectToLaunch),
            ];
        }
        else
        {
            return
            [
                new DevContainerProjectHost("Visual Studio Code", projectToLaunch),
            ];
        }
    }
}
