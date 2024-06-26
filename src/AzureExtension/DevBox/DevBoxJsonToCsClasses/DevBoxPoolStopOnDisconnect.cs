// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents a StopOnDisconnect object within a response from a Dev Box rest API call.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxPoolStopOnDisconnect
{
    public string Status { get; set; } = string.Empty;

    public int GracePeriodMinutes { get; set; }
}
