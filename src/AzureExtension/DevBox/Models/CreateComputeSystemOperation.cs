// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using AzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.Helpers;
using Microsoft.VisualStudio.Services.Commerce;
using Microsoft.VisualStudio.Services.TestManagement.TestPlanning.WebApi;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Models;

public delegate CreateComputeSystemOperation CreateComputeSystemOperationFactory(IDeveloperId developerId, DevBoxCreationParameters userOptions);

/// <summary>
/// Responsible for creating a new DevBox. It is the object return back to Dev Home so Dev Home can track the progress of the operation.
/// </summary>
public class CreateComputeSystemOperation : ICreateComputeSystemOperation
{
    private const string OperationInProgressMessageKey = "DevBox_CreationOperationAlreadyInProgress";

    private const string OperationCompletedMessageKey = "DevBox_CreationOperationAlreadyCompleted";

    private readonly IDevBoxCreationManager _devBoxCreationManager;

    private readonly IDeveloperId _developerId;

    private readonly object _lock = new();

    public event TypedEventHandler<ICreateComputeSystemOperation, CreateComputeSystemActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<ICreateComputeSystemOperation, CreateComputeSystemProgressEventArgs>? OperationProgress;

    public bool IsCompleted { get; private set; }

    public bool IsOperationInProgress { get; private set; }

    public DevBoxCreationParameters DevBoxCreationParameters { get; private set; }

    public CancellationTokenSource CancellationTokenSource { get; private set; } = new();

    public CreateComputeSystemResult? Result { get; private set; }

    public CreateComputeSystemOperation(IDevBoxCreationManager devBoxCreationManager, IDeveloperId developerId, DevBoxCreationParameters parameters)
    {
        _devBoxCreationManager = devBoxCreationManager;
        DevBoxCreationParameters = parameters;
        _developerId = developerId;
    }

    public event TypedEventHandler<ICreateComputeSystemOperation, CreateComputeSystemProgressEventArgs> Progress = (s, e) => { };

    public IAsyncOperation<CreateComputeSystemResult> StartAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                lock (_lock)
                {
                    if (IsOperationInProgress)
                    {
                        var exception = new DevBoxCreationException("Operation already in progress");
                        return new CreateComputeSystemResult(exception, Resources.GetResource(OperationInProgressMessageKey), exception.Message);
                    }
                    else if (IsCompleted)
                    {
                        var exception = new DevBoxCreationException("Operation already completed");
                        return new CreateComputeSystemResult(exception, Resources.GetResource(OperationCompletedMessageKey), exception.Message);
                    }

                    IsOperationInProgress = true;
                }

                // In the future we'll need to add a way to cancel the operation if the Async operation is cancelled from Dev Home.
                // Dev Center in the Dev Portal allows a Dev Box to be deleted while its still in the creation phase. You can think
                // of this as cancelling the operation.
                await _devBoxCreationManager.StartCreateDevBoxOperation(this, _developerId, DevBoxCreationParameters);
            }
            catch (Exception e)
            {
                CompleteWithFailure(e, e.Message);
            }

            return Result!;
        }).AsAsyncOperation();
    }

    public void UpdateProgress(string operationStatus, uint operationProgress)
    {
        if (IsCompleted)
        {
            return;
        }

        OperationProgress?.Invoke(this, new(operationStatus, operationProgress));
    }

    public void CompleteWithFailure(Exception exception, string displayText)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Result ??= new CreateComputeSystemResult(exception, displayText, exception.Message);
        ResetAllSubscriptions();
    }

    public void CompleteWithSuccess(DevBoxInstance devBox)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Result ??= new CreateComputeSystemResult(devBox);
        ResetAllSubscriptions();
    }

    private void ResetAllSubscriptions()
    {
        OperationProgress = null;
    }
}
