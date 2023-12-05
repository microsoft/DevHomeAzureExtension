// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace AzureExtension.DevBox;

public static class Constants
{
    /// <summary>
    /// Azure Resource Graph (ARG) query API
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/en-us/rest/api/azureresourcegraph/resourcegraph/resources/resources?view=rest-azureresourcegraph-resourcegraph-2022-10-01&tabs=HTTP"/>
    public const string ARGQueryAPI = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01";

    /// <summary>
    /// Query to get all projects that a user has access to.
    /// </summary>
    public const string ARGQuery = "{\"query\": \"Resources | where type in~ ('microsoft.devcenter/projects') | where properties['provisioningState'] =~ 'Succeeded' | project id, location, tenantId, name, properties, type\"," +
        " \"options\":{\"allowPartialScopes\":true}}";

    /// <summary>
    /// DevCenter API to get all devboxes
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes/list-dev-boxes-by-user"/>
    public const string DevBoxAPI = "/users/me/devboxes?api-version=2023-07-01-preview";

    /// <summary>
    /// Scope to manage devboxes
    /// </summary>
    public const string DataPlaneScope = "https://devcenter.azure.com/access_as_user";

    /// <summary>
    /// Scope to query for all projects
    /// </summary>
    public const string ManagementPlaneScope = "https://management.azure.com/user_impersonation";

    /// <summary>
    /// API version used for start, stop, and restart APIs
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes?view=rest-devcenter-developer-2023-04-01"/>
    public const string APIVersion = "api-version=2023-04-01";
}
