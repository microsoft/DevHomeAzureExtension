// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Net;
using Moq;
using Moq.Protected;

namespace DevHomeAzureExtension.Test;

[TestClass]
public partial class DevBoxTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> mockFactory = new();

    public TestContext? TestContext
    {
        get; set;
    }

    private TestOptions testOptions = new();

    private TestOptions TestOptions
    {
        get => testOptions;
        set => testOptions = value;
    }

    /// <summary>
    /// This is a mock JSON response from the DevCenter API.
    /// </summary>
    private const string MockTestJson =
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
            ],
                ""value"": [
                {
                    ""uri"": ""https://72f9feed-86f1-41af-91ab-beefd011db47-devcenter-dc.westus3.devcenter.azure.com/projects/nonprod/users/28feedaeb-996b-434b-bb57-4969cbeef8f0/devboxes/two"",
                    ""name"": ""Two"",
                    ""projectName"": ""Nonprod"",
                    ""poolName"": ""Some-Enhanced-Pool-In-Da-West"",
                    ""hibernateSupport"": ""Disabled"",
                    ""provisioningState"": ""Succeeded"",
                    ""actionState"": ""Stopped"",
                    ""powerState"": ""Deallocated"",
                    ""uniqueId"": ""28feedaeb-996b-434b-bb57-4969cbeef8f0"",
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
                ],
            ""facets"": [],
            ""resultTruncated"": ""false"",
            ""rdpConnectionUrl"": ""https://sample.uri.com"",
            ""webUrl"": ""https://sample.uri.com""
        }";

    private HttpClient mockHttpClient = new();

    [TestInitialize]
    public void TestInitialize()
    {
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);

        // Create a mock HttpMessageHandler
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(MockTestJson),
            });

        // Create an HttpClient using the mocked handler
        mockHttpClient = new HttpClient(handlerMock.Object);

        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(mockHttpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
