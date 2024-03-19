// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Helpers;

namespace AzureExtension.DevBox;

public static class Constants
{
    /// <summary>
    /// Azure Resource Graph (ARG) query API
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/rest/api/azureresourcegraph/resourcegraph/resources/resources?view=rest-azureresourcegraph-resourcegraph-2022-10-01&tabs=HTTP"/>
    public const string ARGQueryAPI = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01";

    /// <summary>
    /// Query to get all projects that a user has access to.
    /// </summary>
    public const string ARGQuery = "{\"query\": \"Resources | where type in~ ('microsoft.devcenter/projects') | where properties['provisioningState'] =~ 'Succeeded' | project id, location, tenantId, name, properties, type\"," +
        " \"options\":{\"allowPartialScopes\":true}}";

    /// <summary>
    /// DevCenter API to get all devboxes
    /// </summary>
    /// for stable api's <seealso href="https://learn.microsoft.com/rest/api/devcenter/developer/dev-boxes/list-dev-boxes-by-user"/>
    /// for preview api's <seealso cref="https://github.com/Azure/azure-rest-api-specs/tree/main/specification/devcenter/data-plane/Microsoft.DevCenter/preview"/>
    public const string DevBoxAPI = "/users/me/devboxes?api-version=2023-10-01-preview";

    public const string DevBoxUserSegmentOfUri = "/users/me/devboxes";

    public const string OperationsParameter = "operations";

    /// <summary>
    /// Gets the Regex pattern for the name of a DevBox. This pattern is used to validate the name and the project name of a DevBox before attempting
    /// to create it.
    /// See URI section in https://learn.microsoft.com/rest/api/devcenter/developer/dev-boxes/create-dev-box?view=rest-devcenter-developer-2023-04-01&tabs=HTTP
    /// For more information on the pattern.
    /// </summary>
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
    /// For stable api's <seealso href="https://learn.microsoft.com/rest/api/devcenter/developer/dev-boxes?view=rest-devcenter-developer-2023-04-01"/>
    /// for preview api's <seealso cref="https://github.com/Azure/azure-rest-api-specs/tree/main/specification/devcenter/data-plane/Microsoft.DevCenter/preview"/>
    public const string APIVersion = "api-version=2023-10-01-preview";

    public const string Pools = "pools";

    public const string Projects = "projects";

    public const string Name = "name";

    public const string Properties = "properties";

    public const string DevCenterUri = "devCenterUri";

    public const string PoolNameForCreationRequest = "poolName";

    /// <summary>
    /// We use zero as a signal that the progress is indefinite.
    /// </summary>
    public const uint IndefiniteProgress = 0;

    public static readonly TimeSpan OneMinutePeriod = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan FiveMinutePeriod = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan OperationDeadline = TimeSpan.FromHours(2);

    public const string DevBoxStartOperation = "start";

    public const string DevBoxStopOperation = "stop";

    public const string DevBoxRestartOperation = "restart";

    public const string DevBoxRepairOperation = "repair";

    public const string DevBoxDeletedState = "Deleted";

    public const string DevBoxOperationNotStartedState = "NotStarted";

    public static class DevBoxProvisioningStates
    {
        public const string Succeeded = "Succeeded";

        public const string Failed = "Failed";

        public const string Canceled = "Canceled";

        public const string ProvisionedWithWarning = "ProvisionedWithWarning";

        public const string Provisioning = "Provisioning";

        public const string Creating = "Creating";

        public const string Deleting = "Deleting";

        public const string Updating = "Updating";
    }

    public static class DevBoxActionStates
    {
        public const string Unknown = "Unknown";

        public const string Failed = "Failed";

        public const string Starting = "Starting";

        public const string Started = "Started";

        public const string Stopping = "Stopping";

        public const string Stopped = "Stopped";

        public const string Restarting = "Restarting";

        public const string Repairing = "Repairing";

        public const string Repaired = "Repaired";
    }

    public static class DevBoxPowerStates
    {
        public const string Unknown = "Unknown";

        public const string Stopped = "Stopped";

        public const string Running = "Running";

        public const string Hibernated = "Hibernated";

        public const string Deallocated = "Deallocated";

        public const string PoweredOff = "PoweredOff";
    }

    /// <summary>
    /// The JSON options used to deserialize the DevCenter API responses.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DevCenterOperationStatusConverter() },
        TypeInfoResolver = new JsonSourceGenerationContext(),
    };

    /// <summary>
    /// Location of the thumbnail that is shown for all Dev Boxes in the UI
    /// </summary>
    public const string ThumbnailURI = "ms-appx:///AzureExtension/Assets/DevBoxThumbnail.png";

    /// <summary>
    /// Location of the provider icon.
    /// </summary>
    /// <remarks>
    /// We use different icon locations for different builds. Note these are ms-resource URIs, but are used by Dev Home to load the providers icon,
    /// from the extension package. Extensions that implement the IComputeSystemProvider interface must provide a provider icon in this format.
    /// Dev Home will use SHLoadIndirectString (https://learn.microsoft.com/windows/win32/api/shlwapi/nf-shlwapi-shloadindirectstring) to load the
    /// location of the icon from the extensions package.Once it gets this location, it will load the icon from the path and display it in the UI.
    /// Icons should be located in an extensions resource.pri file which is generated at build time.
    /// See the MakePri.exe documentation for how you can view what is in the resource.pri file, so you can find the location of your icon.
    /// https://learn.microsoft.com/en-us/windows/uwp/app-resources/makepri-exe-command-options. (use MakePri.exe in a VS Developer Command Prompt or
    /// Powershell window)
    /// </remarks>
#if CANARY_BUILD
    public const string ProviderIcon = "ms-resource://Microsoft.Windows.DevHomeAzureExtension.Canary/Files/AzureExtension/Assets/DevBoxProvider.png";
#elif STABLE_BUILD
    public const string ProviderIcon = "ms-resource://Microsoft.Windows.DevHomeAzureExtension/Files/AzureExtension/Assets/DevBoxProvider.png";
#else
    public const string ProviderIcon = "ms-resource://Microsoft.Windows.DevHomeAzureExtension.Dev/Files/AzureExtension/Assets/DevBoxProvider.png";
#endif

    /// <summary>
    /// Name of the DevBox provider
    /// </summary>
    public const string DevBoxProviderId = "Microsoft.DevBox";

    /// <summary>
    /// Non localized display Name for Microsoft Dev Box.
    /// </summary>
    public const string DevBoxProviderDisplayName = "Microsoft DevBox";

    /// <summary>
    /// Size of a GB in bytes
    /// </summary>
    public const ulong BytesInGb = 1073741824UL;

    /// <summary>
    /// Resource key for the error message when a method is not implemented.
    /// </summary>
    public const string DevBoxMethodNotImplementedKey = "DevBox_MethodNotImplementedError";

    /// <summary>
    /// Resource key for the error message the Dev Box extension is unable to perform a requested operation.
    /// </summary>
    public const string DevBoxUnableToPerformOperationKey = "DevBox_UnableToPerformRequestedOperation";
}
