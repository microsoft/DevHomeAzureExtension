// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;

namespace AzureExtension.DevBox.Models;

public class DevBoxOperationResponseHeader
{
    private const string OperationLocationStr = "Operation-Location";

    public Uri? Location { get; set; }

    public Uri? OperationLocation { get; set; }

    /// <summary>
    /// gets the Id for the operation. This is used by the management service to check how long the operation has been running.
    /// </summary>
    public string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Initializes a new instance of the <see cref="DevBoxOperationResponseHeader"/> class.
    /// Dev Box long running operations contain a single header that contain a Location and Operation-Location uri.
    /// these can be used to poll the operation. See <see cref="Constants.APIVersion"/> links for more information.
    /// </summary>
    /// <param name="headers">Header returned from a Dev Center rest Api that expect the operation to be long running</param>
    public DevBoxOperationResponseHeader(HttpResponseHeaders headers)
    {
        Location = headers.Location;

        if (headers.TryGetValues(OperationLocationStr, out var operationLocationValues))
        {
            OperationLocation = new Uri(operationLocationValues.First());
        }
    }
}
