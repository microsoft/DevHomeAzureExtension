// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

public class DevBoxPoolStopOnDisconnect
{
    public string Status { get; set; } = string.Empty;

    public int GracePeriodMinutes { get; set; }
}
