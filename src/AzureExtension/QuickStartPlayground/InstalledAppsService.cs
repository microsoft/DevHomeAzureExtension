// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using DevHomeAzureExtension.Contracts;
using Serilog;
using Windows.Management.Deployment;
using Windows.System.Inventory;

namespace DevHomeAzureExtension.QuickStartPlayground;

public class InstalledAppsService : IInstalledAppsService
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", nameof(InstalledAppsService));

    private static HashSet<string> CheckForMissingMSIXApps(HashSet<string> msixApps)
    {
        var missingPackages = msixApps;

        var pm = new PackageManager();

        foreach (var app in missingPackages)
        {
            var package = pm.FindPackagesForUser(string.Empty, app).FirstOrDefault();
            if (package != null)
            {
                missingPackages.Remove(app);
                if (missingPackages.Count == 0)
                {
                    break;
                }
            }
        }

        return missingPackages;
    }

    private async Task<HashSet<string>> CheckForMissingWin32AppsAsync(HashSet<string> win32Apps)
    {
        var missingApps = win32Apps;

        var desktopApps = await InstalledDesktopApp.GetInventoryAsync();

        foreach (var app in missingApps)
        {
            // This uses .StartsWith as a bit of a hack due to Visual Studio Code having multiple valid entries
            // (e.g., "Microsoft Visual Studio Code (User)", "Microsoft Visual Studio Code Insiders (User)"; presumably there are "(Machine)" versions too)
            if (desktopApps.Any(s => s.DisplayName.StartsWith(app, StringComparison.Ordinal)))
            {
                _log.Information($"Found {app}");
                missingApps.Remove(app);
                if (missingApps.Count == 0)
                {
                    _log.Information("Successfully resolved all Win32 dependencies");
                    break;
                }
            }
        }

        return missingApps;
    }

    private static readonly char[] Separator = ['\r', '\n'];

    private static HashSet<string> CheckForMissingVSCodeExtensions(HashSet<string> vsCodeExtensions)
    {
        _log.Information("Checking for missing VS Code extensions");
        var missingExtensions = vsCodeExtensions;

        var vsCodePath = FindExecutableInPath("code.cmd");
        if (!string.IsNullOrEmpty(vsCodePath))
        {
            var arguments = "--list-extensions";

            // Output of this command will look like this:

            /*
               C:\Users\TestUser>code.cmd --list-extensions
               ms-azuretools.vscode-docker
               ms-dotnettools.csharp
               ms-dotnettools.dotnet-interactive-vscode
               ms-python.python
               ms-vscode-remote.remote-containers
               ms-vscode-remote.remote-wsl
            */

            using var process = new Process();
            process.StartInfo.FileName = vsCodePath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(30000))
            {
                _log.Error("code.cmd did not return after 30 seconds.");
                throw new TimeoutException("code.cmd --list-extensions command didn't return after 30 seconds.");
            }

            var extensions = output.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

            // Check if any of the desired extensions are in the list.
            foreach (var extension in missingExtensions)
            {
                if (extensions.Where(s => s.StartsWith(extension, StringComparison.Ordinal)).Any())
                {
                    _log.Information($"Found extension {extension}");
                    missingExtensions.Remove(extension);
                    if (missingExtensions.Count == 0)
                    {
                        _log.Information("Successfully resolved all Visual Studio Code extension dependencies");
                        break;
                    }
                }
            }
        }
        else
        {
            _log.Error("Unable to find Visual Studio Code's launcher in path.");
        }

        return missingExtensions;
    }

    public static string FindExecutableInPath(string executableName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);

        if (paths != null)
        {
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        _log.Information("${executableName} not found in PATH.");
        return string.Empty;
    }

    private static HashSet<string> ValidateMissingWin32Dependencies(HashSet<string> missingWin32Dependencies, Dictionary<string, List<string>> displayNameAndPathEntries)
    {
        // The Inventory API sometimes claims a dependency is missing when it is in fact installed.
        // To mitigate this, we check the PATH for the dependency.If the dependency is found in the PATH,
        // we remove it from the missing dependencies list.
        foreach (var dependency in missingWin32Dependencies)
        {
            if (!displayNameAndPathEntries.TryGetValue(dependency, out var pathValues))
            {
                _log.Error($"{dependency} not found in display-name-to-path dictionary.");
                throw new ArgumentException($"Missing dependency {dependency} not found in display-name-to-path dictionary.");
            }

            // Iterate over the possible entries in the PATH for the dependency. If any of them are found, remove the dependency from the missing list.
            foreach (var entry in pathValues)
            {
                if (!string.IsNullOrEmpty(FindExecutableInPath(entry)))
                {
                    _log.Information($"PATH contains {entry}");
                    missingWin32Dependencies.Remove(dependency);
                    break;
                }
                else
                {
                    _log.Information($"PATH does not contain {entry}");
                }
            }
        }

        return missingWin32Dependencies;
    }

    public async Task<HashSet<string>> CheckForMissingDependencies(
        HashSet<string> msixPackageFamilies,
        HashSet<string> win32AppDisplayNames,
        HashSet<string> vsCodeExtensions,
        Dictionary<string, List<string>> displayNameAndPathEntries)
    {
        var missingPackages = CheckForMissingMSIXApps(msixPackageFamilies);
        var missingWin32AppsFromInventory = await CheckForMissingWin32AppsAsync(win32AppDisplayNames);
        var missingExtensions = CheckForMissingVSCodeExtensions(vsCodeExtensions);

        var win32AppsWithNoPathEntry = ValidateMissingWin32Dependencies(missingWin32AppsFromInventory, displayNameAndPathEntries);

        return missingPackages.Union(win32AppsWithNoPathEntry).Union(missingExtensions).ToHashSet();
    }
}
