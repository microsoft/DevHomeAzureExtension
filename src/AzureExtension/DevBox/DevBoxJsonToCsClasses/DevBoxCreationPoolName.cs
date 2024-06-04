// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// This is used to represent the json object that we send with the "create Dev Box" request.
/// The only property we need is the pool name.
/// See under Sample request in https://learn.microsoft.com/rest/api/devcenter/developer/dev-boxes/create-dev-box?view=rest-devcenter-developer-2023-04-01&tabs=HTTP#devboxes_createdevbox
/// </summary>
public class DevBoxCreationPoolName
{
    public string PoolName { get; set; } = string.Empty;

    public DevBoxCreationPoolName(string poolName)
    {
        PoolName = poolName;
    }
}
