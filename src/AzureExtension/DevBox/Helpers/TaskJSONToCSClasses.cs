// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.Helpers;

// Example of a task JSON response that will be deserialized into the BaseClass class
//    {
//  "tasks": [
//    {
//      "name": "winget",
//      "parameters": {
//        "inlineConfigurationBase64": ""
//      },
//      "runAs": "User",
//      "id": "d473decc-0e0e-4452-9a17-f93e4fe5fc2b",
//      "logUri": ",
//      "status": "Succeeded",
//      "startTime": "2024-04-09T20:09:03.0135721+00:00",
//      "endTime": "2024-04-09T20:09:25.047521+00:00"
//    },
//    {
//    "name": "winget",
//      "parameters": {
//        "inlineConfigurationBase64": ""
//      },
//      "runAs": "User",
//      "id": "760eb6f5-fb72-452f-9b3c-d0c189b78c00",
//      "logUri": "",
//      "status": "Succeeded",
//      "startTime": "2024-04-09T20:09:25.8467306+00:00",
//      "endTime": "2024-04-09T20:09:38.5302446+00:00"
//    }
//  ],
//  "uri": "",
//  "name": "AzureExt133571668748121674",
//  "status": "Running",
//  "startTime": "2024-04-09T20:09:03.0136738+00:00"
// }
//

/// <summary>
/// Represents the classes for the customization task JSON response.
/// </summary>
public class TaskJSONToCSClasses
{
    public class BaseClass
    {
        public List<TaskItem> Tasks { get; set; } = new();

        public string Uri { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string StartTime { get; set; } = string.Empty;
    }

    public class TaskItem
    {
        public string Name { get; set; } = string.Empty;

        public Parameters? Parameters { get; set; }

        public string RunAs { get; set; } = string.Empty;

        public string Id { get; set; } = string.Empty;

        public string LogUri { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string StartTime { get; set; } = string.Empty;

        public string EndTime { get; set; } = string.Empty;
    }

    public class Parameters
    {
        public string InlineConfigurationBase64 { get; set; } = string.Empty;
    }
}
