// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Contracts;

using Windows.ApplicationModel;

public interface IPackagesService
{
    public bool IsPackageInstalled(string packageName);

    public PackageVersion GetPackageInstalledVersion(string packageName);
}
