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
    /// Action to validate the URL provided by the user.
    /// </summary>
    Submit,

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
    /// Action to initiate the user Moved a tile up in the configuration.
    /// </summary>
    MoveTileUp,

    /// <summary>
    /// Action to initiate the user Moved a tile down in the configuration.
    /// </summary>
    MoveTileDown,

    /// <summary>
    /// Action to initiate the user Deleted a specific tile in the configuration.
    /// </summary>
    DeleteTile,

    /// <summary>
    /// Action to save the updated configuration.
    /// </summary>
    Save,

    /// <summary>
    /// Action to exit the configuration flow and discard changes.
    /// </summary>
    Cancel,
}
