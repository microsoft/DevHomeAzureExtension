// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;

namespace AzureExtension.Test.DevBox;

internal class AuthTestService : IDevBoxAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;

    public AuthTestService(IHttpClientFactory httpClientFactory, IArmTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
    }

    public HttpClient GetDataPlaneClient()
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dataTokenService.GetTokenAsync().Result);
        return httpClient;
    }

    public HttpClient GetManagementClient()
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armTokenService.GetTokenAsync().Result);
        return httpClient;
    }
}
