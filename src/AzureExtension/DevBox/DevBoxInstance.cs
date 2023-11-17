// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Diagnostics;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxInstance : IComputeSystem
{
    public string Name
    {
        get;
    }

    public string Id
    {
        get;
    }

    public string ProjectName
    {
        get;
    }

    public string State
    {
        get;
    }

    public string CPU
    {
        get;
    }

    public string Memory
    {
        get;
    }

    public string WebURI
    {
        get;
    }

    public string BoxURI
    {
        get;
    }

    public string RdpURI
    {
        get;
    }

    public string OS
    {
        get;
    }

    // Todo: Make this a singleton
    private HttpClient HttpClient
    {
        get;
    }

    public DevBoxInstance(string name, string id, string project, string state, string cpu, string memory, string boxUri, string webUri, string rdpUri, string os, HttpClient httpClient)
    {
        Name = name;
        Id = id;
        ProjectName = project;
        State = state;
        CPU = cpu;
        Memory = memory;
        BoxURI = boxUri;
        WebURI = webUri;
        RdpURI = rdpUri;
        OS = os;
        HttpClient = httpClient;
    }

    public ComputeSystemOperations SupportedOperations => ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown;

    public IAsyncOperation<ComputeSystemOperationResult> Start(string options)
    {
        return Task.Run(async () =>
        {
            var api = BoxURI + ":start?api-version=2023-04-01";
            var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, api));
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
            var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, api));
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
        return Task.Run(async () =>
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = WebURI;
            Process.Start(psi);
            await Task.Delay(10);
            var res = new ComputeSystemOperationResult("Success");
            return res;
        }).AsAsyncOperation();
    }

    // Unsupported operations
    public IAsyncOperation<ComputeSystemStateResult> GetState(string options) => throw new NotImplementedException();

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
