// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Represents the data returned from a request to retrieve the remote connection information for a Dev Box.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxRemoteConnectionData
{
    public string? WebUrl { get; set; }

    public string? RdpConnectionUrl { get; set; }

    // Example - ms-cloudpc:connect?cpcid={cloud-pc-id}&username={username}&environment={environment}&version={version}&rdlaunchurl={optional-rd-launch-url}
    public string? CloudPcConnectionUrl { get; set; }
}
