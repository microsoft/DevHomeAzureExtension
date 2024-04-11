// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.Contracts;

public interface IPackagesService
{
    public bool IsPackageInstalled(string packageName);
}
