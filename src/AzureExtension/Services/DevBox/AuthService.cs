// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class AuthService : IDevBoxAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;
    private readonly HttpClient httpArmClient;
    private readonly HttpClient httpDataClient;

    public AuthService(IHttpClientFactory httpClientFactory, IArmTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
        httpArmClient = _httpClientFactory.CreateClient();
        httpDataClient = _httpClientFactory.CreateClient();
    }

    public HttpClient GetDataPlaneClient(IDeveloperId? devId)
    {
        httpDataClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dataTokenService.GetTokenAsync(devId).Result);
        return httpDataClient;
    }

    public HttpClient GetManagementClient(IDeveloperId? devId)
    {
        httpArmClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armTokenService.GetTokenAsync().Result);
        return httpArmClient;
    }
}
