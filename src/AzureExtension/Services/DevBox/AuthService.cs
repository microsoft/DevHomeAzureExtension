// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using DevHomeAzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Windows.ApplicationModel;

namespace DevHomeAzureExtension.Services.DevBox;

public class AuthService : IDevBoxAuthService
{
    // ARM = Azure Resource Manager
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IArmTokenService _armTokenService;
    private readonly IDataTokenService _dataTokenService;
    private readonly HttpClient _httpArmClient;
    private readonly HttpClient _httpDataClient;

    private bool IsTest()
    {
        try
        {
            return Package.Current.Id.Name.Contains("Test");
        }
        catch
        {
            return true;
        }
    }

    public AuthService(IHttpClientFactory httpClientFactory, IArmTokenService armTokenService, IDataTokenService dataTokenService)
    {
        _httpClientFactory = httpClientFactory;
        _armTokenService = armTokenService;
        _dataTokenService = dataTokenService;
        _httpArmClient = _httpClientFactory.CreateClient();
        _httpDataClient = _httpClientFactory.CreateClient();

        if (!IsTest())
        {
            var version = Package.Current.Id.Version;
            var versionString = version.Build + "." + version.Major + "." + version.Minor + "." + version.Revision;
            var agentHeader = new ProductInfoHeaderValue(Package.Current.Id.Name, versionString);
            _httpArmClient.DefaultRequestHeaders.UserAgent.Add(agentHeader);
            _httpDataClient.DefaultRequestHeaders.UserAgent.Add(agentHeader);
        }
    }

    /// <summary>
    /// Returns a HTTPClient with the authentication header needed to
    /// make calls to the data plane of the Dev Box service.
    /// </summary>
    /// <param name="devId">DeveloperId to use for the authentication token</param>
    public HttpClient GetDataPlaneClient(IDeveloperId? devId)
    {
        _httpDataClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dataTokenService.GetTokenAsync(devId).Result);
        return _httpDataClient;
    }

    /// <summary>
    /// Returns a HTTPClient with the authentication header needed to
    /// make calls to the management plane of the Dev Box service.
    /// </summary>
    /// <param name="devId">DeveloperId to use for the authentication token</param>
    public HttpClient GetManagementClient(IDeveloperId? devId)
    {
        // ARM = Azure Resource Manager
        _httpArmClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _armTokenService.GetTokenAsync(devId).Result);
        return _httpArmClient;
    }
}
