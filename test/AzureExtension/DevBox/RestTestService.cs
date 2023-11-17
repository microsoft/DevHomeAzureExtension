// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using AzureExtension.Contracts;

namespace AzureExtension.Test.DevBox;

public class RestTestService : IDevBoxRESTService
{
    public async Task<JsonElement> GetAllProjectsAsJSONAsync()
    {
        JsonElement result;

        var jsonString =
            @"{
                ""totalRecords"": 10,
                ""count"": 10,
                ""data"": [
                {
                  ""id"": ""/subscriptions/beedbc7420-fb03-44af-daac-69d8ff6e4e8c/resourceGroups/sales_devbox_rg/providers/Microsoft.DevCenter/projects/BasicDevBox"",
                  ""location"": ""westus3"",
                  ""tenantId"": ""72f999bf-86f1-41af-96fc-2d7beef3db47"",
                  ""name"": ""BasicDevBox"",
                  ""properties"": {
                    ""provisioningState"": ""Succeeded"",
                    ""description"": ""Plain devbox, just VS 2022, tools,  no enlistment"",
                    ""devCenterUri"": ""https://72f98fab-86f1-41af-69ab-2dbeef11db47-Salesdevcenter.devcenter.azure.com/"",
                    ""devCenterId"": ""/subscriptions/beedbc7420-fb03-44af-daac-69d8ff6e4e8c/resourceGroups/sales_devbox_rg/providers/Microsoft.DevCenter/devcenters/SalesDevCenter""
                  },
                  ""type"": ""microsoft.devcenter/projects""
                }
                ],
                ""facets"": [],
                ""resultTruncated"": ""false""
            }";

        var json = JsonDocument.Parse(jsonString).RootElement;
        await Task.Delay(0);
        json.TryGetProperty("data", out result);
        return result;
    }

    public async Task<JsonElement> GetBoxesAsJSONAsync(string devCenterUri, string project)
    {
        JsonElement result;

        var jsonString =
            @"{
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
                ]
            }
            ";

        var json = JsonDocument.Parse(jsonString).RootElement;
        await Task.Delay(0);
        json.TryGetProperty("value", out result);
        return result;
    }
}
