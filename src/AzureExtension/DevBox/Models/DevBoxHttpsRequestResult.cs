// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace DevHomeAzureExtension.DevBox.Models;

/// <summary>
/// Represents the result of an HTTPS request result from the Dev Center. For Dev Box operations
/// the only things we care about is the JSON response and the response header.
/// </summary>
public class DevBoxHttpsRequestResult
{
    public JsonElement JsonResponseRoot { get; set; }

    public DevBoxOperationResponseHeader ResponseHeader { get; set; }

    public DevBoxHttpsRequestResult(JsonElement jsonResponseRoot, DevBoxOperationResponseHeader responseHeader)
    {
        JsonResponseRoot = jsonResponseRoot;
        ResponseHeader = responseHeader;
    }
}
