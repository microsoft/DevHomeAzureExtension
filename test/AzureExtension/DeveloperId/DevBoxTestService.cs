// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using AzureExtension.Contracts;

namespace AzureExtension.Services.DeveloperBox;

public class DevBoxTestService : IDevBoxRESTService
{
    public JsonElement GetAllProjects() => throw new NotImplementedException();

    public JsonElement GetBoxes()
    {
        var jsonString =
            @"{
              ""Class Name"": ""Science"",
              ""Teacher\u0027s Name"": ""Jane"",
              ""Semester"": ""2019-01-01"",
              ""Students"": [
                {
                  ""Name"": ""John"",
                  ""Grade"": 94.3
                },
                {
                  ""Name"": ""James"",
                  ""Grade"": 81.0
                },
                {
                  ""Name"": ""Julia"",
                  ""Grade"": 91.9
                },
                {
                  ""Name"": ""Jessica"",
                  ""Grade"": 72.4
                },
                {
                  ""Name"": ""Johnathan""
                }
              ],
              ""Final"": true
            }
            ";

        JsonElement json = JsonDocument.Parse(jsonString).RootElement;

        return json;
    }
}
