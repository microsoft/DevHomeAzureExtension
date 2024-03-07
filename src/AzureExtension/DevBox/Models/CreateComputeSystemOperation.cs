// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using AzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Models;

public delegate CreateComputeSystemOperation CreateComputeSystemOperationFactory(IDeveloperId developerId, DevBoxCreationParameters userOptions);

/// <summary>
/// Responsible for creating a new DevBox. It is the object return back to Dev Home so Dev Home can track the progress of the operation.
/// </summary>
public class CreateComputeSystemOperation : ICreateComputeSystemOperation
{
#pragma warning disable IDE0044 // Add readonly modifier
    private CreateComputeSystemResult _result;
#pragma warning restore IDE0044 // Add readonly modifier

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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public CreateComputeSystemOperation(IDevBoxCreationManager devBoxCreationManager, IDeveloperId developerId, DevBoxCreationParameters parameters)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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
                        return _result;
                    }

                    IsOperationInProgress = true;
                }

                // In the future we'll need to add a way to cancel the operation if the Async operation is cancelled from Dev Home.
                // Dev Center in the Dev Portal allows a Dev Box to be deleted while its still in the creation phase. You can think
                // of this as cancelling the operation.
                _result = await _devBoxCreationManager.StartCreateDevBoxOperation(this, _developerId, DevBoxCreationParameters);
            }
            catch (Exception ex)
            {
                _result = new CreateComputeSystemResult(ex, Resources.GetResource(OperationCompletedMessageKey), ex.Message);
            }

            // Now that the operation is complete, we can reset the operation state.
            lock (_lock)
            {
                IsOperationInProgress = false;
                IsCompleted = true;
                OperationProgress = null;
            }

            return _result;
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
}
