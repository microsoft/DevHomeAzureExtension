// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;

namespace DevHomeAzureExtension.DevBox.Models;

/// <summary>
/// Represents the header for a Dev Box long running operation. The Dev Box APIs run a Location and an Operation-Location
/// value in the header. These can be used to poll the progress of the operation.
/// See API documentation <see cref="Constants.APIVersion"/>
/// </summary>
public class DevBoxOperationResponseHeader
{
    private const string OperationLocationStr = "Operation-Location";

    public Uri? Location { get; set; }

    public Uri? OperationLocation { get; set; }

    /// <summary>
    /// gets the Id for the operation. This is used by the<see cref="DevBoxOperationWatcherTimer"/> when checking how long the operation has been running.
    /// </summary>
    public string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Initializes a new instance of the <see cref="DevBoxOperationResponseHeader"/> class.
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
