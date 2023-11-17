// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Text.Json;

namespace AzureExtension.Contracts;

public interface IDevBoxRESTService
{
    JsonElement GetAllProjects();

    JsonElement GetBoxes();
}
