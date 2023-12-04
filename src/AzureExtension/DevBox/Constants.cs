// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace AzureExtension.DevBox;

public static class Constants
{
    // Azure Resource Graph (ARG) query API
    public const string ARGQueryAPI = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01";

    // Query to get all projects
    public const string ARGQuery = "{\"query\": \"Resources | where type in~ ('microsoft.devcenter/projects') | where properties['provisioningState'] =~ 'Succeeded' | project id, location, tenantId, name, properties, type\"," +
        " \"options\":{\"allowPartialScopes\":true}}";

    // DevCenter API to get all devboxes
    // Link : https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes/list-dev-boxes-by-user
    public const string DevBoxAPI = "/users/me/devboxes?api-version=2023-07-01-preview";

    // Scope to manage devboxes
    public const string DataPlaneScope = "https://devcenter.azure.com/access_as_user";

    // Scope to query for all projects
    public const string ManagementPlaneScope = "https://management.azure.com/user_impersonation";

    // API version used for start, stop, and restart APIs
    public const string APIVersion = "api-version=2023-04-01";
}
