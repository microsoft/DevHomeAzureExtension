// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using AzureExtension.Contracts;

namespace AzureExtension.Test.DevBox;

public class DevBoxTestService : IDevBoxRESTService
{
    public async Task<JsonElement> GetAllProjectsAsJSONAsync()
    {
        var jsonString = @"
            ""totalRecords"": 17,
            ""count"": 17,
            ""data"": [
            {
              ""id"": ""/subscriptions/bddbc709-fb03-44af-acad-68d8ff6e4a8c/resourceGroups/o365core_devbox_rg/providers/Microsoft.DevCenter/projects/DevBoxBasic"",
              ""location"": ""westus3"",
              ""tenantId"": ""72f988bf-86f1-41af-91ab-2d7cd011db47"",
              ""name"": ""DevBoxBasic"",
              ""properties"": {
                ""provisioningState"": ""Succeeded"",
                ""description"": ""Plain devbox, just VS 2022, tools,  no enlistment"",
                ""devCenterUri"": ""https://72f988bf-86f1-41af-91ab-2d7cd011db47-o365devcenter.devcenter.azure.com/"",
                ""devCenterId"": ""/subscriptions/bddbc709-fb03-44af-acad-68d8ff6e4a8c/resourceGroups/o365core_devbox_rg/providers/Microsoft.DevCenter/devcenters/O365DevCenter""
              },
              ""type"": ""microsoft.devcenter/projects""
            }
            ],
            ""facets"": [],
            ""resultTruncated"": ""false""";

        var json = JsonDocument.Parse(jsonString).RootElement;
        await Task.Delay(0);
        return json;
    }

    public async Task<JsonElement> GetBoxesAsJSONAsync(string devCenterUri, string project)
    {
        var jsonString =
            @"{
                ""value"": [
                {
                    ""uri"": ""https://72f988bf-86f1-41af-91ab-2d7cd011db47-devcenter-f7ssrn3hcydbm-dc.westus3.devcenter.azure.com/projects/engprodadept/users/28aebba8-996b-434b-bb57-4969c097c8f0/devboxes/two"",
                    ""name"": ""Two"",
                    ""projectName"": ""EngProdADEPT"",
                    ""poolName"": ""WE-ADEPT-Enhanced-Pool-West"",
                    ""hibernateSupport"": ""Disabled"",
                    ""provisioningState"": ""Succeeded"",
                    ""actionState"": ""Stopped"",
                    ""powerState"": ""Deallocated"",
                    ""uniqueId"": ""239a099d-d97d-4caa-935d-46027a448d73"",
                    ""location"": ""westus2"",
                    ""osType"": ""Windows"",
                    ""user"": ""28aebba8-996b-434b-bb57-4969c097c8f0"",
                    ""hardwareProfile"": {
                    ""vCPUs"": 16,
                    ""skuName"": ""general_a_16c64gb2048ssd_v2"",
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
        return json;
    }
}
