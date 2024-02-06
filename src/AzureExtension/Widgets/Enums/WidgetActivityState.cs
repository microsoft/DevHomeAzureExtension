// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Widgets.Enums;

public enum WidgetActivityState
{
    /// <summary>
    /// Error state and default initialization. This state is a clue something went terribly wrong.
    /// </summary>
    Unknown,

    /// <summary>
    /// Widget is in this state after it is created and before it has data assigned to it,
    /// or when the Configure option has been selected.
    /// </summary>
    Configure,

    /// <summary>
    /// Widget cannot do more until signed in.
    /// </summary>
    SignIn,

    /// <summary>
    /// Widget is configured and it is assumed user can interact and see it.
    /// </summary>
    Active,

    /// <summary>
    /// Widget is in good state, but host is minimized or disables the widget.
    /// </summary>
    Inactive,

    /// <summary>
    /// Widget has been marked for deletion and should not be used or do anything.
    /// </summary>
    Deleted,
}
