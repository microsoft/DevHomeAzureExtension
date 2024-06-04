// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Windows.ApplicationModel;

namespace DevHomeAzureExtension.Services.DevBox;

public class PackagesService : IPackagesService
{
    private Windows.Management.Deployment.PackageManager _packageManager = new();

    public bool IsPackageInstalled(string packageName)
    {
        var currentPackage = _packageManager.FindPackagesForUser(string.Empty, packageName).FirstOrDefault();
        return currentPackage != null;
    }

    public PackageVersion GetPackageInstalledVersion(string packageName)
    {
        PackageVersion version = new PackageVersion(0, 0, 0, 0);
        var currentPackage = _packageManager.FindPackagesForUser(string.Empty, packageName).FirstOrDefault();
        if (currentPackage != null)
        {
            version = currentPackage.Id.Version;
        }

        return version;
    }
}
