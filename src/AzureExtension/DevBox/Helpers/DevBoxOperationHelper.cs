// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Helpers;

public static class DevBoxOperationHelper
{
    public static string GetDevBoxOperationName(DevBoxOperation action)
    {
        switch (action)
        {
            case DevBoxOperation.Start:
                return Constants.DevBoxStartOperation;
            case DevBoxOperation.Stop:
                return Constants.DevBoxStopOperation;
            case DevBoxOperation.Restart:
                return Constants.DevBoxRestartOperation;
            default:
                return string.Empty;
        }
    }
}
