// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using AzureExtension.Contracts;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AzureExtension.DevBox;

/// <summary>
/// As the actual implementation of IComputeSystem, DevBoxInstance is an instance of a DevBox.
/// It contains the DevBox details such as name, id, state, CPU, memory, and OS. And all the
/// operations that can be performed on the DevBox.
/// </summary>
public class DevBoxInstance : IComputeSystem
{
    public IDeveloperId? DevId
    {
        get; private set;
    }

    public string? Name
    {
        get; private set;
    }

    public string? Id
    {
        get; private set;
    }

    public string? ProjectName
    {
        get; private set;
    }

    public string? State
    {
        get; private set;
    }

    public string? CPU
    {
        get; private set;
    }

    public string? Memory
    {
        get; private set;
    }

    public Uri? WebURI
    {
        get; private set;
    }

    public Uri? BoxURI
    {
        get; private set;
    }

    public Uri? RdpURI
    {
        get; private set;
    }

    public string? OS
    {
        get; private set;
    }

    public IEnumerable<ComputeSystemProperty> Properties { get; set; } = new List<ComputeSystemProperty>();

    private string? DiskSize
    {
        get; set;
    }

    private readonly IDevBoxAuthService _authService;

    public event TypedEventHandler<IComputeSystem, ComputeSystemState>? StateChanged;

    public DevBoxInstance(IDevBoxAuthService devBoxAuthService)
    {
        _authService = devBoxAuthService;
    }

    private void ProcessProperties()
    {
        if (!Properties.Any())
        {
            var properties = new List<ComputeSystemProperty>();
            try
            {
                if (CPU is not null)
                {
                    var cpu = new ComputeSystemProperty(int.Parse(CPU, CultureInfo.CurrentCulture), ComputeSystemPropertyKind.CpuCount);
                    properties.Add(cpu);
                }

                if (Memory is not null)
                {
                    var memoryInGB = int.Parse(Memory, CultureInfo.CurrentCulture);
                    var memoryInBytes = memoryInGB * Constants.BytesInGb;
                    var memory = new ComputeSystemProperty(memoryInBytes, ComputeSystemPropertyKind.AssignedMemorySizeInBytes);
                    properties.Add(memory);
                }

                if (DiskSize is not null)
                {
                    var diskSizeInGB = int.Parse(DiskSize, CultureInfo.CurrentCulture);
                    var diskSizeInBytes = diskSizeInGB * Constants.BytesInGb;
                    var diskSize = new ComputeSystemProperty(diskSizeInBytes, ComputeSystemPropertyKind.StorageSizeInBytes);
                    properties.Add(diskSize);
                }

                Properties = properties;
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error processing properties for {Name}: {ex.ToString}");
            }
        }
    }

    /// <summary>
    /// Fills the DevBoxInstance from the JSON returned from the DevBox API.
    /// This includes the box name, id, state, CPU, memory, and OS.
    /// </summary>
    /// <param name="item">JSON that contains the dev box details</param>
    /// <param name="project">Project under which the dev box exists</param>
    /// <param name="devId">Developer ID for the dev box</param>
    public void FillFromJson(JsonElement item, string project, IDeveloperId? devId)
    {
        item.TryGetProperty("uri", out var uri);
        BoxURI = new Uri(uri.ToString());
        item.TryGetProperty("name", out var name);
        Name = name.ToString();
        item.TryGetProperty("id", out var id);
        Id = id.ToString();
        item.TryGetProperty("powerState", out var powerState);
        State = powerState.ToString();

        if (item.TryGetProperty("hardwareProfile", out var hardwareProfile))
        {
            hardwareProfile.TryGetProperty("vCPUs", out var vCPUs);
            CPU = vCPUs.ToString();
            hardwareProfile.TryGetProperty("memoryGB", out var memoryGB);
            Memory = memoryGB.ToString();
        }

        if (item.TryGetProperty("storageProfile", out var storageProfile))
        {
            if (storageProfile.TryGetProperty("osDisk", out var diskSizeGB))
            {
                if (diskSizeGB.TryGetProperty("diskSizeGB", out var diskSize))
                {
                    DiskSize = diskSize.ToString();
                }
            }
        }

        if (item.TryGetProperty("imageReference", out var imageReference))
        {
            if (imageReference.TryGetProperty("operatingSystem", out var operatingSystem))
            {
                OS = operatingSystem.ToString();
            }
        }

        ProjectName = project;
        DevId = devId;
        AssociatedDeveloperId = devId ?? AssociatedDeveloperId;

        try
        {
            (WebURI, RdpURI) = GetRemoteLaunchURIsAsync(BoxURI).GetAwaiter().GetResult();
            Log.Logger()?.ReportInfo($"Created box {Name} with id {Id} with {State}, {CPU}, {Memory}, {BoxURI}, {OS}");
            IsValid = true;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Error making DevBox from JSON: {ex.ToString}");
        }
    }

    private async Task<(Uri? WebURI, Uri? RdpURI)> GetRemoteLaunchURIsAsync(Uri boxURI)
    {
        var connectionUri = $"{boxURI}/remoteConnection?{Constants.APIVersion}";
        var boxRequest = new HttpRequestMessage(HttpMethod.Get, connectionUri);
        var httpClient = _authService.GetDataPlaneClient(DevId);
        if (httpClient == null)
        {
            return (null, null);
        }

        var boxResponse = await httpClient.SendAsync(boxRequest);
        var content = await boxResponse.Content.ReadAsStringAsync();
        JsonElement json = JsonDocument.Parse(content).RootElement;
        var remoteUri = new Uri(json.GetProperty("rdpConnectionUrl").ToString());
        var webUrl = new Uri(json.GetProperty("webUrl").ToString());
        return (webUrl, remoteUri);
    }

    public ComputeSystemOperations SupportedOperations =>
        ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown | ComputeSystemOperations.Delete | ComputeSystemOperations.Restart;

    public bool IsValid
    {
        get;
        private set;
    }

    // To Do: Move this to a resource file
    public string AlternativeDisplayName => new("Project : " + ProjectName ?? "Unknown");

    public IDeveloperId AssociatedDeveloperId
    {
        get => DevId ?? throw new NotImplementedException();
        set => DevId = value;
    }

    public string AssociatedProviderId => Constants.DevBoxProviderName;

    /// <summary>
    /// Common method to perform REST operations on the DevBox.
    /// This method is used by Start, ShutDown, Restart, and Delete.
    /// </summary>
    /// <param name="operation">Operation to be performed</param>
    /// <param name="method">HttpMethod to be used</param>
    /// <returns>ComputeSystemOperationResult created from the result of the operation</returns>
    private IAsyncOperation<ComputeSystemOperationResult> PerformRESTOperation(string operation, HttpMethod method)
    {
        return Task.Run(async () =>
        {
            try
            {
                var api = operation.Length > 0 ?
                    $"{BoxURI}:{operation}?{Constants.APIVersion}" : $"{BoxURI}?{Constants.APIVersion}";
                Log.Logger()?.ReportInfo($"Starting {Name} with {api}");

                var httpClient = _authService.GetDataPlaneClient(DevId);
                if (httpClient == null)
                {
                    var ex = new ArgumentException("PerformRESTOperation: HTTPClient null");
                    return new ComputeSystemOperationResult(ex, string.Empty);
                }

                var response = await httpClient.SendAsync(new HttpRequestMessage(method, api));
                if (response.IsSuccessStatusCode)
                {
                    var res = new ComputeSystemOperationResult();
                    return res;
                }
                else
                {
                    var ex = new HttpRequestException($"PerformRESTOperation: {operation} failed on {Name}: {response.StatusCode} {response.ReasonPhrase}");
                    return new ComputeSystemOperationResult(ex, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"PerformRESTOperation: Exception: {operation} failed on {Name}: {ex.ToString}");
                return new ComputeSystemOperationResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> StartAsync(string options)
    {
        // ToDo: Change this event
        StateChanged?.Invoke(this, ComputeSystemState.Starting);
        return PerformRESTOperation("start", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> ShutDownAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Stopping);
        return PerformRESTOperation("stop", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> RestartAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Restarting);
        return PerformRESTOperation("restart", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteAsync(string options)
    {
        StateChanged?.Invoke(this, ComputeSystemState.Deleting);
        return PerformRESTOperation(string.Empty, HttpMethod.Delete);
    }

    /// <summary>
    /// Launches the DevBox in a browser.
    /// </summary>
    /// <param name="options">Unused parameter</param>
    public IAsyncOperation<ComputeSystemOperationResult> ConnectAsync(string options)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = WebURI?.ToString();
                Process.Start(psi);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error connecting to {Name}: {ex.ToString}");
                return new ComputeSystemOperationResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> TerminateAsync(string options)
    {
        return ShutDownAsync(options);
    }

    public IAsyncOperation<ComputeSystemStateResult> GetStateAsync(string options)
    {
        return Task.Run(() =>
        {
            try
            {
                if (State == "Running")
                {
                    return new ComputeSystemStateResult(ComputeSystemState.Running);
                }
                else if (State == "Deallocated")
                {
                    return new ComputeSystemStateResult(ComputeSystemState.Stopped);
                }
                else
                {
                    Log.Logger()?.ReportError($"Unknown state {State}");
                    return new ComputeSystemStateResult(ComputeSystemState.Unknown);
                }
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error getting state of {Name}: {ex.ToString}");
                return new ComputeSystemStateResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemThumbnailResult> GetComputeSystemThumbnailAsync(string options)
    {
        return Task.Run(async () =>
        {
            try
            {
                var uri = new Uri(Constants.ThumbnailURI);
                var storageFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                var randomAccessStream = await storageFile.OpenReadAsync();

                // Convert the stream to a byte array
                var bytes = new byte[randomAccessStream.Size];
                await randomAccessStream.ReadAsync(bytes.AsBuffer(), (uint)randomAccessStream.Size, InputStreamOptions.None);

                return new ComputeSystemThumbnailResult(bytes);
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error getting thumbnail for {Name}: {ex.ToString}");
                return new ComputeSystemThumbnailResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<IEnumerable<ComputeSystemProperty>> GetComputeSystemPropertiesAsync(string options)
    {
        return Task.Run(() =>
        {
            ProcessProperties();
            return Properties;
        }).AsAsyncOperation();
    }

    // Unsupported operations
    public IAsyncOperation<ComputeSystemOperationResult> RevertSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> CreateSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteSnapshotAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> PauseAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ResumeAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> SaveAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ModifyPropertiesAsync(string options)
    {
        return Task.Run(() =>
        {
            return new ComputeSystemOperationResult(new NotImplementedException(), "Method not implemented");
        }).AsAsyncOperation();
    }

    public IApplyConfigurationOperation ApplyConfiguration(string configuration) => throw new NotImplementedException();
}
