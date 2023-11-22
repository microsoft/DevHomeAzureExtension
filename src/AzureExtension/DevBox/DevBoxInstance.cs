// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxInstance : IComputeSystem
{
    private readonly IDevBoxAuthService _authService;

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

    public string? WebURI
    {
        get; private set;
    }

    public string? BoxURI
    {
        get; private set;
    }

    public string? RdpURI
    {
        get; private set;
    }

    public string? OS
    {
        get; private set;
    }

    public DevBoxInstance(IDevBoxAuthService devBoxAuthService)
    {
        _authService = devBoxAuthService;
    }

    // Returns a DevBox object from a JSON object
    public void FillFromJson(JsonElement item, string project, IDeveloperId? devId)
    {
        try
        {
            BoxURI = item.GetProperty("uri").ToString();
            Name = item.GetProperty("name").ToString();
            Id = item.GetProperty("uniqueId").ToString();
            State = item.GetProperty("powerState").ToString();
            CPU = item.GetProperty("hardwareProfile").GetProperty("vCPUs").ToString();
            Memory = item.GetProperty("hardwareProfile").GetProperty("memoryGB").ToString();
            OS = item.GetProperty("imageReference").GetProperty("operatingSystem").ToString();
            ProjectName = project;
            DevId = devId;

            (WebURI, RdpURI) = GetRemoteLaunchURIsAsync(BoxURI).Result;
            Log.Logger()?.ReportInfo($"Created box {Name} with id {Id} with {State}, {CPU}, {Memory}, {BoxURI}, {OS}");
            IsValid = true;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Error making DevBox from JSON: {ex.Message}");
        }
    }

    private async Task<(string WebURI, string RdpURI)> GetRemoteLaunchURIsAsync(string boxURI)
    {
        var connectionUri = boxURI + "/remoteConnection?api-version=2023-04-01";
        var boxRequest = new HttpRequestMessage(HttpMethod.Get, connectionUri);
        var httpClient = _authService.GetDataPlaneClient(DevId);
        if (httpClient == null)
        {
            return (string.Empty, string.Empty);
        }

        var boxResponse = httpClient.Send(boxRequest);
        var content = await boxResponse.Content.ReadAsStringAsync();
        JsonElement json = JsonDocument.Parse(content).RootElement;
        var remoteUri = json.GetProperty("rdpConnectionUrl").ToString();
        var webUrl = json.GetProperty("webUrl").ToString();
        return (webUrl, remoteUri);
    }

    public ComputeSystemOperations SupportedOperations => ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown;

    public bool IsValid
    {
        get;
        private set;
    }

    public IAsyncOperation<ComputeSystemOperationResult> Start(string options)
    {
        return Task.Run(async () =>
        {
            var api = BoxURI + ":start?api-version=2023-04-01";
            Log.Logger()?.ReportInfo($"Starting {Name} with {api}");

            var httpClient = _authService.GetDataPlaneClient(DevId);
            if (httpClient == null)
            {
                return new ComputeSystemOperationResult("Fail");
            }

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, api));
            if (response.IsSuccessStatusCode)
            {
                var res = new ComputeSystemOperationResult("Success");
                return res;
            }
            else
            {
                /*
                 {StatusCode: 404, ReasonPhrase: 'Not Found', Version: 1.1, Content: System.Net.Http.HttpConnectionResponseContent, Headers:
                    {
                        Date: Mon, 06 Nov 2023 22:05:23 GMT
                        Connection: keep-alive
                        x-ms-correlation-request-id: 83d5aa04-5411-4097-9ebc-c5ec2e734aac
                        x-ms-client-request-id: 13b8ea9d-d252-4fe2-8254-6d6f2039a016
                        X-Rate-Limit-Limit: 1m
                        X-Rate-Limit-Remaining: 299
                        X-Rate-Limit-Reset: 2023-11-06T22:06:23.7468234Z
                        Strict-Transport-Security: max-age=15724800; includeSubDomains
                        Content-Length: 0
                    }}
                */
                return new ComputeSystemOperationResult("Fail");
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> ShutDown(string options)
    {
        return Task.Run(async () =>
        {
            var api = BoxURI + ":stop?api-version=2023-04-01";
            Log.Logger()?.ReportInfo($"Shutting down {Name} with {api}");

            var httpClient = _authService.GetDataPlaneClient(DevId);
            if (httpClient == null)
            {
                return new ComputeSystemOperationResult("Fail");
            }

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, api));
            if (response.IsSuccessStatusCode)
            {
                var res = new ComputeSystemOperationResult("Success");
                return res;
            }
            else
            {
                return new ComputeSystemOperationResult("Fail");
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> Connect(string properties)
    {
        // Todo: Correct this to remove delay
        return Task.Run(() =>
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = WebURI;
            Process.Start(psi);
            var res = new ComputeSystemOperationResult("Success");
            return res;
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemStateResult> GetState(string options)
    {
        return Task.Run(() =>
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
        }).AsAsyncOperation();
    }

    // Unsupported operations
    public IAsyncOperation<ComputeSystemOperationResult> ApplyConfiguration(string configuration) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> ApplySnapshot(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> CreateSnapshot(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Delete(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> DeleteSnapshot(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> GetProperties(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> ModifyProperties(string properties) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Pause(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Resume(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Save(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Terminate(string options) => throw new NotImplementedException();
}
