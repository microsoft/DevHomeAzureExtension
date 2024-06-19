// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.DevBox.Models;
using AzureExtension.Services.DevBox;
using AzureExtension.Test.DevBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Moq.Language;
using Moq.Protected;

namespace DevHomeAzureExtension.Test;

[TestClass]
public partial class DevBoxTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockFactory = new();

    public Mock<HttpMessageHandler> HttpHandler { get; set; } = new(MockBehavior.Strict);

    public IHost? TestHost { get; set; }

    public TestContext? TestContext
    {
        get; set;
    }

    private TestOptions _testOptions = new();

    private TestOptions TestOptions
    {
        get => _testOptions;
        set => _testOptions = value;
    }

    /// <summary>
    /// This is a mock JSON response from the DevCenter API.
    /// </summary>
    private const string MockProjectJson =
            @"{
            ""totalRecords"": 10,
            ""count"": 10,
            ""data"": [
            {
              ""id"": ""/subscriptions/beedbc7420-fb03-44af-daac-69d8ff6e4e8c/resourceGroups/sales_devbox_rg/providers/Microsoft.DevCenter/projects/DevBoxUnitTestProject"",
              ""location"": ""westus3"",
              ""tenantId"": ""72f999bf-86f1-41af-96fc-2d7beef3db47"",
              ""name"": ""DevBoxUnitTestProject"",
              ""properties"": {
                ""provisioningState"": ""Succeeded"",
                ""description"": ""Plain devbox, just VS 2022, tools,  no enlistment"",
                ""devCenterUri"": ""https://72f98fab-86f1-41af-69ab-2dbeef11db47-Salesdevcenter.devcenter.azure.com/"",
                ""devCenterId"": ""/subscriptions/beedbc7420-fb03-44af-daac-69d8ff6e4e8c/resourceGroups/sales_devbox_rg/providers/Microsoft.DevCenter/devcenters/SalesDevCenter""
              },
              ""type"": ""microsoft.devcenter/projects""
            }
            ]
        }";

    private const string MockDevBoxListJson =
           @"{ ""value"": [
                {
                    ""uri"": ""https://72f9feed-86f1-41af-91ab-beefd011db47-devcenter-dc.westus3.devcenter.azure.com/projects/nonprod/users/28feedaeb-996b-434b-bb57-4969cbeef8f0/devboxes/two"",
                    ""name"": ""Two"",
                    ""projectName"": ""Nonprod"",
                    ""poolName"": ""Some-Enhanced-Pool-In-Da-West"",
                    ""hibernateSupport"": ""Disabled"",
                    ""provisioningState"": ""Succeeded"",
                    ""actionState"": ""Stopped"",
                    ""powerState"": ""Deallocated"",
                    ""uniqueId"": ""28feedae-996b-434b-bb57-4969cbeef8f0"",
                    ""location"": ""westus2"",
                    ""osType"": ""Windows"",
                    ""user"": ""28feedaeb-996b-434b-bb57-4969cbeef8f0"",
                    ""hardwareProfile"": {
                    ""vCPUs"": 16,
                    ""skuName"": ""some_sku_v2"",
                    ""memoryGB"": 64
                    },
                    ""storageProfile"": {
                    ""osDisk"": {
                        ""diskSizeGB"": 2048
                    }
                    },
                    ""imageReference"": {
                    ""name"": ""1es-office-enhancedtools-baseImage"",
                    ""version"": ""2023.0608.0"",
                    ""operatingSystem"": ""Windows 11 Enterprise"",
                    ""osBuildNumber"": ""22H2"",
                    ""publishedDate"": ""2023-06-08T19:48:22.7178982+00:00""
                    },
                    ""createdTime"": ""2023-08-22T19:06:51.0750942+00:00"",
                    ""localAdministrator"": ""Enabled""
                }
            ]
        }";

    private const string MockTestOperationJson =
        @"{
              ""id"": ""3c0b24dd-53e3-48a1-889b-83088048a1fc"",
              ""name"": ""3c0b24dd-53e3-48a1-889b-83088048a1fc"",
              ""status"": ""Running"",
              ""startTime"": ""2024-02-20T08:23:16.8547869+00:00""
        }";

    private const string MockTestPoolJson =
        @"{
          ""uri"": ""https://8a40af38-3b4c-4672-a6a4-5e964b1870ed-contosodevcenter.centralus.devcenter.azure.com/projects/myProject/users/b08e39b4-2ac6-4465-a35e-48322efb0f98/devboxes/MyDevBox"",
          ""name"": ""MyDevBox"",
          ""provisioningState"": ""Succeeded"",
          ""projectName"": ""ContosoProject"",
          ""poolName"": ""LargeDevWorkStationPool"",
          ""location"": ""centralus"",
          ""osType"": ""Windows"",
          ""user"": ""b08e39b4-2ac6-4465-a35e-48322efb0f98"",
          ""hardwareProfile"": {
            ""vCPUs"": 8,
            ""memoryGB"": 32
          },
          ""storageProfile"": {
            ""osDisk"": {
              ""diskSizeGB"": 1024
            }
          },
          ""hibernateSupport"": ""Enabled"",
          ""imageReference"": {
            ""name"": ""DevImage"",
            ""version"": ""1.0.0"",
            ""publishedDate"": ""2022-03-01T00:13:23.323Z""
          }
        }";

    private const string MockTestRemoteConnectionJson =
        @"{
              ""webUrl"": ""https://devcenter.azure.com/projects/project/users/b214d34a-3feb-12ab-96bd-cca70d0c9d69/devboxes/DevBox1"",
              ""rdpConnectionUrl"": ""3c0b24dd-53e3-48a1-889b-83088048a1fc""
        }";

    private const string MockTestCreationParametersJson =
        @"{
              ""NewEnvironmentName"": ""MyDevBox"",
              ""projectName"": ""MyProject"",
              ""poolName"": ""MyPoolName"",
              ""devCenterUri"": ""https://devcenter.azure.com""
        }";

    public static HttpResponseHeaders GetHeadersMock()
    {
        var requestMessage = new HttpResponseMessage();
        var headers = requestMessage.Headers;
        headers.Add("Operation-Location", "https://devcenter.azure.com/projects/project/projects/engprodadept/operationStatuses/3c0b24dd-53e3-48a1-889b-83088048a1fc");
        headers.Add("Location", "https://devcenter.azure.com/projects/project/projects/engprodadept/operationStatuses/3c0b24dd-53e3-48a1-889b-83088048a1fc");
        return headers;
    }

    private HttpClient _mockHttpClient = new();

    public void UpdateHttpClientResponseMock(List<HttpContent> returnList)
    {
        var handlerSequence = HttpHandler
           .Protected()
           .SetupSequence<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>());

        foreach (var item in returnList)
        {
            handlerSequence = AddResponse(handlerSequence, item);
        }
    }

    private ISetupSequentialResult<Task<HttpResponseMessage>> AddResponse(ISetupSequentialResult<Task<HttpResponseMessage>> handlerSequence, HttpContent content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };

        foreach (var header in GetHeadersMock())
        {
            response.Headers.Add(header.Key, header.Value);
        }

        return handlerSequence.ReturnsAsync(response);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);
        TestHelpers.ConfigureTestLog(TestOptions, TestContext!);

        // Create an HttpClient using the mocked handler
        _mockHttpClient = new HttpClient(HttpHandler.Object);

        _mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_mockHttpClient);

        TestHost = BuildHostContainer();
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestHelpers.CloseTestLog();
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public IHost BuildHostContainer()
    {
        return Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateOnBuild = true;
            }).
            ConfigureServices((context, services) =>
            {
                services.AddSingleton<IHttpClientFactory>(_mockFactory.Object);
                services.AddSingleton<HttpClient>(_mockHttpClient);
                services.AddSingleton<IDevBoxManagementService, DevBoxManagementService>();
                services.AddSingleton<IDevBoxAuthService, AuthService>();
                services.AddSingleton<IArmTokenService, ArmTestTokenService>();
                services.AddSingleton<IDataTokenService, DataTestTokenService>();
                services.AddSingleton<IPackagesService, PackagesService>();
                services.AddSingleton<DevBoxProvider>();

                services.AddSingleton<ITimeSpanService, TimeSpanServiceMock>();
                services.AddSingleton<IDevBoxOperationWatcher, DevBoxOperationWatcher>();
                services.AddSingleton<IDevBoxCreationManager, DevBoxCreationManager>();
                services.AddSingleton<DevBoxInstanceFactory>(sp => (developerId, dexBoxMachine) => ActivatorUtilities.CreateInstance<DevBoxInstance>(sp, developerId, dexBoxMachine));
                services.AddSingleton<CreateComputeSystemOperationFactory>(sp => (devId, userOptions) => ActivatorUtilities.CreateInstance<CreateComputeSystemOperation>(sp, devId, userOptions));
            })
            .Build();
    }
}
