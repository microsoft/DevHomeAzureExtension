// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the data returned from a request to retrieve the remote connection information for a Dev Box.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxRemoteConnectionData
{
    public string? WebUrl { get; set; }

    public string? RdpConnectionUrl { get; set; }
}
