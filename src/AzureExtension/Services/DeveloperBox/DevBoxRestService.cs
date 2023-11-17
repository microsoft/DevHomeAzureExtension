// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using AzureExtension.Contracts;

namespace AzureExtension.Services.DeveloperBox;

public class DevBoxRestService : IDevBoxRESTService
{
    public JsonElement GetAllProjects() => throw new NotImplementedException();

    public JsonElement GetBoxes() => throw new NotImplementedException();
}
