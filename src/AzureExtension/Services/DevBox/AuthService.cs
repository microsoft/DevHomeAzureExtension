// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class AuthService : IDevBoxAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;

    public AuthService(IHttpClientFactory httpClientFactory, IArmTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
    }

    public HttpClient GetDataPlaneClient(IDeveloperId? devId)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dataTokenService.GetTokenAsync(devId).Result);
        return httpClient;
    }

    public HttpClient GetManagementClient(IDeveloperId? devId)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armTokenService.GetTokenAsync().Result);
        return httpClient;
    }
}
