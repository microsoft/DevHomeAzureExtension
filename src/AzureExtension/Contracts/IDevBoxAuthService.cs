// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Contracts;

public interface IDevBoxAuthService
{
    HttpClient GetManagementClient(IDeveloperId? devId);

    HttpClient GetDataPlaneClient(IDeveloperId? devId);
}
