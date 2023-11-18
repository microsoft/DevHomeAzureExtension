// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;

namespace AzureExtension.Services.DevBox;

public class AuthService : IDevBoxAuthService
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
