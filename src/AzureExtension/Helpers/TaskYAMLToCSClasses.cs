﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.Helpers;

// Example of a YAML file that will be deserialized into the BasePackage class
//    properties:
//      resources:
//      - resource: Microsoft.WinGet.DSC/BasePackage
//        directives:
//          description: Installing SublimeHQ.SublimeText.4
//          allowPrerelease: true
//        settings:
//          id: "SublimeHQ.SublimeText.4"
//          source: winget
//        id: 'SublimeHQ.SublimeText.4 | Install: Sublime Text 4'
//      - resource: Microsoft.WinGet.DSC/BasePackage
//        directives:
//          description: Installing Git
//          allowPrerelease: true
//        settings:
//          id: "Git.Git"
//          source: winget
//        id: Git.Git
//      - resource: GitDsc/GitClone
//        directives:
//          description: 'Cloning: devhome.git'
//          allowPrerelease: true
//        settings:
//          httpsUrl: https://github.com/microsoft/devhome.git
//          rootDirectory: C:\Users\Public\repos\devhome.git
//        id: 'Clone devhome.git: C:\Users\Public\repos\devhome.git'
//        dependsOn:
//        - 'Git.Git | Install: Git'
//      configurationVersion: 0.2.0
//
/// <summary>
/// Represents the classes for the YAML customization task.
/// </summary>
public class TaskYAMLToCSClasses
{
    public class BasePackage
    {
        public Properties? Properties { get; set; }
    }

    public class Properties
    {
        public string ConfigurationVersion { get; set; } = string.Empty;

        public List<ResourceItem>? Resources { get; set; }

        public void SetResources(List<ResourceItem>? resources) => Resources = resources;
    }

    public class ResourceItem
    {
        public string Resource { get; set; } = string.Empty;

        public Directives? Directives { get; set; }

        public Settings? Settings { get; set; }

        public string Id { get; set; } = string.Empty;
    }

    public class Directives
    {
        public string Description { get; set; } = string.Empty;

        public bool? AllowPrerelease { get; set; }
    }

    public class Settings
    {
        public string Id { get; set; } = string.Empty;

        public string? HttpsUrl { get; set; }

        public string? RootDirectory { get; set; }

        public string Source { get; set; } = string.Empty;
    }
}
