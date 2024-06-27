// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Helpers;

// Example of a task JSON response that will be deserialized into the BaseClass class
// {
//  "abilitiesAsAdmin": [],
//  "abilitiesAsDeveloper": [
//    "ReadDevBoxes",
//    "WriteDevBoxes",
//    ...
//  ]
// }
//

/// <summary>
/// Represents the class for the abilities JSON response.
/// </summary>
public class AbilitiesJSONToCSClasses
{
    public class BaseClass
    {
        public List<string> AbilitiesAsAdmin { get; set; } = new();

        public List<string> AbilitiesAsDeveloper { get; set; } = new();
    }
}
