// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using AzureExtension.DevBox;
using AzureExtension.DevBox.DevBoxJsonToCsClasses;
using AzureExtension.DevBox.Models;
using Microsoft.Windows.DevHome.SDK;

namespace AzureExtension.Contracts;

public interface IDevBoxManagementService
{
    /// <summary>
    /// Makes an Http request to the azure management plane of the Dev Center.
    /// </summary>
    public Task<HttpRequestResult> HttpRequestToManagementPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent);

    /// <summary>
    /// Makes an Http request to the azure data plane of the Dev Center.
    /// </summary>
    public Task<HttpRequestResult> HttpRequestToDataPlane(Uri webUri, IDeveloperId developerId, HttpMethod method, HttpContent? requestContent);

    /// <summary>
    /// Generates a list of object that each contain a Dev Center project and the pools associated with the project.
    /// </summary>
    /// <param name="projectsJson">The Json recieved from a rest api that returns a list of Dev Center projects.</param>
    /// <param name="developerId">The DeveloperId associated with the request.</param>
    /// <returns>A list of objects where each contain a project and its associated pools.</returns>
    public Task<List<DevBoxProjectAndPoolContainer>> GetAllProjectsToPoolsMappingAsync(JsonElement projectsJson, IDeveloperId developerId);

    /// <summary>
    /// Method used to create a Dev Box in the Dev Center.
    /// </summary>
    public Task<HttpRequestResult> CreateDevBox(DevBoxCreationParameters parameters, IDeveloperId developerId);
}
