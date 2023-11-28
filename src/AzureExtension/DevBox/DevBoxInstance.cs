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
    public void FillFromJson(JsonElement item, string project, IDeveloperId? devId) => throw new NotImplementedException();

    // ToDo: Use System.Uri
    private (string WebURI, string RdpURI) GetRemoteLaunchURIsAsync(string boxURI)
    {
         return (string.Empty, string.Empty);
    }

    public ComputeSystemOperations SupportedOperations => ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown;

    public bool IsValid
    {
        get;
        private set;
    }

    private IAsyncOperation<ComputeSystemOperationResult> PerformRESTOperation(string operation) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> Start(string options)
    {
        return PerformRESTOperation("start");
    }

    public IAsyncOperation<ComputeSystemOperationResult> ShutDown(string options)
    {
        return PerformRESTOperation("stop");
    }

    public IAsyncOperation<ComputeSystemOperationResult> Connect(string properties) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemStateResult> GetState(string options) => throw new NotImplementedException();

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
