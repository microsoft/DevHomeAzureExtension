// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Threading;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using AzureExtension.Services.DevBox;
using AzureExtension.Test.DevBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

public partial class DevBoxTests
{
    private readonly AutoResetEvent _autoResetEvent = new(false);

    public ComputeSystemState ActualState { get; set; }

    public void OnStateChange(IComputeSystem sender, ComputeSystemState state)
    {
        ActualState = state;
    }

    public void OnProgress(ICreateComputeSystemOperation op, CreateComputeSystemProgressEventArgs data)
    {
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DevBox_GetAllDevBoxes()
    {
        // Arrange
        var contentList = new List<HttpContent>
        {
            new StringContent(MockProjectJson),
            new StringContent(MockDevBoxListJson),
        };
        UpdateHttpClientResponseMock(contentList);

        // Act
        var provider = TestHost!.Services.GetService<DevBoxProvider>();
        var systems = provider?.GetDevBoxesAsync(new EmptyDeveloperId()).Result;

        // Assert
        Assert.IsTrue(systems?.Any());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DevBox_CreateDevBox()
    {
        // Arrange
        var devBoxList = JsonSerializer.Deserialize<DevBoxMachines>(MockDevBoxListJson, Constants.JsonOptions);
        devBoxList!.Value![0].ProvisioningState = "Creating";
        var devBoxJsonBeingCreated = JsonSerializer.Serialize(devBoxList!.Value![0], Constants.JsonOptions);
        devBoxList!.Value![0].ProvisioningState = "Succeeded";
        var devBoxJsonSucceeded = JsonSerializer.Serialize(devBoxList!.Value![0], Constants.JsonOptions);

        var contentList = new List<HttpContent>
        {
            new StringContent(devBoxJsonBeingCreated), // creation call output
            new StringContent(devBoxJsonSucceeded), // creation succeeded.
        };

        UpdateHttpClientResponseMock(contentList);

        // Act
        var provider = TestHost!.Services.GetService<DevBoxProvider>();
        var operation = provider!.CreateCreateComputeSystemOperation(new EmptyDeveloperId(), MockTestCreationParametersJson) as CreateComputeSystemOperation;
        operation!.Progress += OnProgress;
        var result = await operation.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5)); // wait for the operation to complete
        operation.Progress -= OnProgress;

        // Assert
        Assert.IsTrue(result.Result.Status == ProviderOperationStatus.Success);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task DevBox_DevBoxOperationsSucceed()
    {
        // Arrange
        var devBoxList = JsonSerializer.Deserialize<DevBoxMachines>(MockDevBoxListJson, Constants.JsonOptions);
        devBoxList!.Value![0].PowerState = Constants.DevBoxPowerStates.PoweredOff;
        var devBoxListPoweredOffJson = JsonSerializer.Serialize(devBoxList!, Constants.JsonOptions);
        devBoxList!.Value![0].PowerState = Constants.DevBoxPowerStates.Running;
        var runningDevBox = devBoxList!.Value![0];
        var runningDevBoxJson = JsonSerializer.Serialize(runningDevBox, Constants.JsonOptions);
        var operationCompletedSucceeded = JsonSerializer.Deserialize<DevBoxOperation>(MockTestOperationJson, Constants.JsonOptions);
        operationCompletedSucceeded!.Status = DevCenterOperationStatus.Succeeded;
        var succeededJson = JsonSerializer.Serialize(operationCompletedSucceeded!, Constants.JsonOptions);

        var contentList = new List<HttpContent>
        {
            new StringContent(MockProjectJson),
            new StringContent(devBoxListPoweredOffJson),
            new StringContent(MockTestOperationJson), // initial operation status with 'Running' status
            new StringContent(succeededJson), // ending operation status with 'Succeeded' status
            new StringContent(runningDevBoxJson), // ending operation status with 'Succeeded' status
        };

        UpdateHttpClientResponseMock(contentList);

        // Act
        var provider = TestHost!.Services.GetService<DevBoxProvider>();
        var systems = provider?.GetDevBoxesAsync(new EmptyDeveloperId()).Result;
        var devBox = systems?.FirstOrDefault();

        Assert.IsNotNull(devBox);
        var currentState = await devBox!.GetStateAsync();
        Assert.IsTrue(currentState.State == ComputeSystemState.Stopped);

        devBox!.StateChanged += OnStateChange;
        var result = await devBox.StartAsync(string.Empty);
        Assert.IsTrue(result.Result.Status == ProviderOperationStatus.Success);
        var retries = 5u;

        while (retries > 0 && ActualState != ComputeSystemState.Running)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            retries--;
        }

        devBox!.StateChanged -= OnStateChange;

        // Assert
        Assert.AreEqual(ComputeSystemState.Running, ActualState);
    }
}
