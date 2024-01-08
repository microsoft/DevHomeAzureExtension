// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension.Widgets.Enums;

public enum WidgetAction
{
    /// <summary>
    /// Error condition where the action cannot be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Action to initiate the user Sign-In.
    /// </summary>
    SignIn,

    /// <summary>
    /// Action to initiate the user Added a tile in the configuration.
    /// </summary>
    AddTile,

    /// <summary>
    /// Action to initiate the user Removed a tile in the configuration.
    /// </summary>
    RemoveTile,

    /// <summary>
    /// Action to save the updated configuration.
    /// </summary>
    Save,

    /// <summary>
    /// Action to exit the configuration flow and discard changes.
    /// </summary>
    Cancel,
}
