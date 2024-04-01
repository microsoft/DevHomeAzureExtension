// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Helpers;

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
}
