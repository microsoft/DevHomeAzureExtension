// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Widgets;

public enum WidgetDataState
{
    Unknown,
    Requested,      // Request is out, waiting on a response. Current data is stale.
    Okay,           // Received and updated data, stable state.
    FailedUpdate,   // Failed updating data.
    FailedRead,     // Failed to read the data.
    Disposed,       // DataManager has been disposed and should not be used.
}
