// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.Helpers;

// Example of a task JSON response that will be deserialized into the BaseClass class
// {
//  "abilitiesAsAdmin": [],
//  "abilitiesAsDeveloper": [
//    "CustomizeDevBoxes",
//    "DeleteDevBoxes",
//    "ManageDevBoxActions",
//    "ReadDevBoxActions",
//    "ReadDevBoxes",
//    "ReadRemoteConnections",
//    "StartDevBoxes",
//    "StopDevBoxes",
//    "WriteDevBoxes"
//  ]
// }
//

/// <summary>
/// Represents the classes for the customization task JSON response.
/// </summary>
public class AbilitiesJSONToCSClasses
{
    public class BaseClass
    {
        public List<string> AbilitiesAsAdmin { get; set; } = new();

        public List<string> AbilitiesAsDeveloper { get; set; } = new();
    }
}
