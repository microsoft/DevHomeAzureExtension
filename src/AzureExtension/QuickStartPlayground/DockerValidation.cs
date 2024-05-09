// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Windows.Storage;

namespace AzureExtension.QuickStartPlayground;

public static class DockerValidation
{
    public static bool IsDockerRunning()
    {
        return Process.GetProcessesByName("docker").Length != 0;
    }

    public static async Task<bool> BuildDockerContainerAsync(StorageFolder outputFolder, DataReceivedEventHandler dataReceivedEventHandler)
    {
        // Set up a new process to launch docker in the devcontainer folder
        using var docker = new Process();
        docker.StartInfo.UseShellExecute = false;
        docker.StartInfo.CreateNoWindow = true;
        docker.StartInfo.FileName = "docker.exe";
        docker.StartInfo.WorkingDirectory = Path.Combine(outputFolder.Path, ".devcontainer");
        docker.StartInfo.Arguments = "build .";
        docker.StartInfo.RedirectStandardOutput = true;
        docker.StartInfo.RedirectStandardError = true;

        // Get the output of the process so that we can stream it back to the UI
        docker.OutputDataReceived += dataReceivedEventHandler;
        docker.ErrorDataReceived += dataReceivedEventHandler;

        docker.Start();

        docker.BeginOutputReadLine();
        docker.BeginErrorReadLine();

        await docker.WaitForExitAsync();
        return docker.ExitCode == 0;
    }
}
