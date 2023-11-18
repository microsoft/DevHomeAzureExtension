// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;

namespace AzureExtension.Services.DeveloperBox;

internal class AuthService : IDevBoxAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;

    public AuthService(IHttpClientFactory httpClientFactory, IDataTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
    }

    public HttpClient GetDataPlaneClient() => throw new NotImplementedException();

    public HttpClient GetManagementClient() => throw new NotImplementedException();
}
