// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace AzureExtension.DevBox.Models;

public class DevBoxOperationResult
{
    public JsonElement JsonResponseRoot { get; set; }

    public DevBoxOperationResponseHeader ResponseHeader { get; set; }

    public DevBoxOperationResult(JsonElement jsonResponseRoot, DevBoxOperationResponseHeader responseHeader)
    {
        JsonResponseRoot = jsonResponseRoot;
        ResponseHeader = responseHeader;
    }
}
