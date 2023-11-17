// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;

namespace AzureExtension.Services.DeveloperBox;

internal class AuthService : IDevBoxAuthService
{
    private readonly IHost _host;

    public AuthService(IHost host)
    {
        _host = host;
    }

    public HttpClient GetDataPlaneClient() => throw new NotImplementedException();

    public HttpClient GetManagementClient() => throw new NotImplementedException();
}
