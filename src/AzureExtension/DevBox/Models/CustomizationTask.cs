// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Helpers;

namespace AzureExtension.DevBox.Models;

public class CustomizationTask
{
    public bool IsGitCloneTask { get; set; }

    public string Description { get; private set; }

    public TaskYAMLToCSClasses.ResourceItem ResourceItem { get; set; }

    public CustomizationTask(string desc, TaskYAMLToCSClasses.ResourceItem resource, bool isGitCloneTask) =>
        (Description, ResourceItem, IsGitCloneTask) = (desc, resource, isGitCloneTask);
}
