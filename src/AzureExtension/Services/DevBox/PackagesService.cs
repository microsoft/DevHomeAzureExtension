// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

public class PackagesService
{
    private Windows.Management.Deployment.PackageManager _packageManager = new();

    public bool IsPackageInstalled(string packageName)
    {
        var currentPackage = _packageManager.FindPackagesForUser(string.Empty, packageName).FirstOrDefault();
        return currentPackage != null;
    }
}
