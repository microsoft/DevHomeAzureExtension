// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;
using DevHomeAzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Contracts;

public interface IDevBoxManagementService
{
    /// <summary>
    /// Makes an Https request to the azure management plane of the Dev Center.
    /// </summary>
    /// <param name="webUri">The Uri of the request.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    /// <param name="method">The type of the the http request. E.g Get, Put, Post etc.</param>
    /// <param name="requestContent">The content that should be used with the request.</param>
    /// <returns>The result of the request.</returns>
    public Task<DevBoxHttpsRequestResult> HttpsRequestToManagementPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent);

    /// <summary>
    /// Makes an Https request to the azure data plane of the Dev Center.
    /// </summary>
    /// <param name="webUri">The Uri of the request.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    /// <param name="method">The type of the the http request. E.g Get, Put, Post etc.</param>
    /// <param name="requestContent">The content that should be used with the request.</param>
    /// <returns>The result of the request.</returns>
    public Task<DevBoxHttpsRequestResult> HttpsRequestToDataPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent);

    /// <summary>
    /// Makes an Https request to the azure data plane of the Dev Center.
    /// </summary>
    /// <param name="webUri">The Uri of the request.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    /// <param name="method">The type of the the http request. E.g Get, Put, Post etc.</param>
    /// <returns>The string result of the request.</returns>
    public Task<string> HttpsRequestToDataPlaneRawResponse(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent = null);

    /// <summary>
    /// Generates a list of objects that each contain a Dev Center project and the Dev Box pools associated with that project.
    /// </summary>
    /// <param name="projects">The Deserialized Json received from a rest api that returns a list of Dev Center projects.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    /// <returns>A list of objects where each contain a project and its associated pools.</returns>
    public Task<List<DevBoxProjectAndPoolContainer>> GetAllProjectsToPoolsMappingAsync(DevBoxProjects projects, IDeveloperId developerId);

    /// <summary>
    /// Initiates a call to create a Dev Box in the Dev Center.
    /// </summary>
    /// <param name="parameters">The parameters used to create the Dev Box.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    public Task<DevBoxHttpsRequestResult> CreateDevBox(DevBoxCreationParameters parameters, IDeveloperId developerId);
}
