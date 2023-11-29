// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net.Http.Headers;
using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Services.DevBox;

public class AuthService : IDevBoxAuthService
{
    // ARM = Azure Resource Manager
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;
    private readonly HttpClient _httpArmClient;
    private readonly HttpClient _httpDataClient;

    public AuthService(IHttpClientFactory httpClientFactory, IArmTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
        _httpArmClient = _httpClientFactory.CreateClient();
        _httpDataClient = _httpClientFactory.CreateClient();
    }

    public HttpClient GetDataPlaneClient(IDeveloperId? devId)
    {
        _httpDataClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dataTokenService.GetTokenAsync(devId).Result);
        return _httpDataClient;
    }

    public HttpClient GetManagementClient(IDeveloperId? devId)
    {
        // ARM = Azure Resource Manager
        _httpArmClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armTokenService.GetTokenAsync(devId).Result);
        return _httpArmClient;
    }
}
