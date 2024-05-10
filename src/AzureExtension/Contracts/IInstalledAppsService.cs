// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.Contracts;

public interface IInstalledAppsService
{
    public Task<HashSet<string>> CheckForMissingDependencies(
        HashSet<string> msixPackageFamilies,
        HashSet<string> win32AppDisplayNames,
        HashSet<string> vsCodeExtensions,
        Dictionary<string, List<string>> displayNameAndPathEntries);
}
