// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

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
    /// for stable api's <seealso href="https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes/list-dev-boxes-by-user"/>
    /// for preview api's <seealso cref="https://github.com/Azure/azure-rest-api-specs/tree/main/specification/devcenter/data-plane/Microsoft.DevCenter/preview"/>
    public const string DevBoxAPI = "/users/me/devboxes?api-version=2023-10-01-preview";

    public const string DevBoxUserSegmentOfUri = "/users/me/devboxes";

    public const string NameRegexPattern = @"^[a-zA-Z0-9][a-zA-Z0-9-_.]{2,62}$";

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
    /// For stable api's <seealso href="https://learn.microsoft.com/en-us/rest/api/devcenter/developer/dev-boxes?view=rest-devcenter-developer-2023-04-01"/>
    /// for preview api's <seealso cref="https://github.com/Azure/azure-rest-api-specs/tree/main/specification/devcenter/data-plane/Microsoft.DevCenter/preview"/>
    public const string APIVersion = "api-version=2023-10-01-preview";

    public const string Pools = "pools";

    public const string Projects = "projects";

    public const string Name = "name";

    public const string Properties = "properties";

    public const string DevCenterUri = "devCenterUri";

    public const string PoolNameForCreationRequest = "poolName";

    public const uint IndefiniteProgress = 0;

    public static readonly TimeSpan OneMinutePeriod = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan FiveMinutePeriod = TimeSpan.FromMinutes(5);

    public const string DevBoxStartOperation = "Start";

    public const string DevBoxStopOperation = "Stop";

    public const string DevBoxRestartOperation = "Restart";

    public const string DevBoxCreatingProvisioningState = "Creating";

    public const string DevBoxProvisioningState = "Provisioning";

    public const string DevBoxProvisioningFailedState = "ProvisioningFailed";

    public const string DevBoxUnknownState = "Unknown";

    public const string DevBoxRunningState = "Running";

    public const string DevBoxDeallocatedState = "Deallocated";

    public const string DevBoxPoweredOffState = "PoweredOff";

    public const string DevBoxHibernatedState = "Hibernated";

    public const string DevBoxOperationNotStartedState = "NotStarted";

    public const string DevBoxOperationSucceededState = "Succeeded";

    public const string DevBoxOperationCanceledState = "Canceled";

    public const string DevBoxOperationFailedState = "Failed";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Location of the thumbnail that is shown for all Dev Boxes in the UI
    /// </summary>
    public const string ThumbnailURI = "ms-appx:///AzureExtension/Assets/DevBoxThumbnail.png";

    /// <summary>
    /// Location of the provider icon
    /// </summary>
    public const string ProviderURI = "ms-appx:///AzureExtension/Assets/DevBoxProvider.png";

    /// <summary>
    /// Name of the DevBox provider
    /// </summary>
    public const string DevBoxProviderName = "Microsoft.DevBox";

    /// <summary>
    /// Non localized display Name for Microsoft Dev Box.
    /// </summary>
    public const string DevBoxProviderDisplayName = "Microsoft DevBox";

    /// <summary>
    /// Size of a GB in bytes
    /// </summary>
    public const ulong BytesInGb = 1073741824UL;
}
