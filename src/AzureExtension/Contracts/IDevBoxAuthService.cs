// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IDevBoxAuthService
{
    HttpClient GetManagementClient(IDeveloperId? devId);

    HttpClient GetDataPlaneClient(IDeveloperId? devId);
}
