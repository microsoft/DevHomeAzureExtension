// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.DevBox.Helpers;

public static class DevBoxOperationHelper
{
    public static string ActionToPerformToString(DevBoxActionToPerform action)
    {
        return action switch
        {
            DevBoxActionToPerform.Start => Constants.DevBoxStartOperation,
            DevBoxActionToPerform.Stop => Constants.DevBoxStopOperation,
            DevBoxActionToPerform.Restart => Constants.DevBoxRestartOperation,
            _ => string.Empty,
        };
    }

    public static DevBoxActionToPerform? StringToActionToPerform(string? actionName)
    {
        return actionName switch
        {
            Constants.DevBoxStartOperation => DevBoxActionToPerform.Start,
            Constants.DevBoxStopOperation => DevBoxActionToPerform.Stop,
            Constants.DevBoxRestartOperation => DevBoxActionToPerform.Restart,
            Constants.DevBoxRepairOperation => DevBoxActionToPerform.Repair,
            _ => null,
        };
    }

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public static ConfigurationUnitState JSONStatusToUnitStatus(string status)
    {
        return status switch
        {
            "NotStarted" => ConfigurationUnitState.Pending,
            "Running" => ConfigurationUnitState.InProgress,
            "Skipped" => ConfigurationUnitState.Skipped,
            "Succeeded" => ConfigurationUnitState.Completed,
            "Failed" => ConfigurationUnitState.Unknown,
            "TimedOut" => ConfigurationUnitState.Unknown,
            "WaitingForUserSession" => ConfigurationUnitState.Pending,
            _ => ConfigurationUnitState.Unknown,

            // Not implemented by the REST API
            // "WaitingForUserInputUac" => ConfigurationUnitState.Unknown,
        };
    }

    public static ConfigurationSetState JSONStatusToSetStatus(string status)
    {
        return status switch
        {
            "NotStarted" => ConfigurationSetState.Pending,
            "Running" => ConfigurationSetState.InProgress,
            "Succeeded" => ConfigurationSetState.Completed,
            "Failed" => ConfigurationSetState.Unknown,
            "ValidationFailed" => ConfigurationSetState.Unknown,
            _ => ConfigurationSetState.Unknown,
        };
    }
}
