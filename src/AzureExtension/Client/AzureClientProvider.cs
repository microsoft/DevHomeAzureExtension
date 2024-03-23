// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;

namespace DevHomeAzureExtension.Client;

public class AzureClientProvider
{
    private static readonly Lazy<ILogger> _log = new(() => Serilog.Log.ForContext("SourceContext", nameof(AzureClientProvider)));

    private static readonly ILogger Log = _log.Value;

    private static VssConnection? CreateConnection(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Information($"Cannot Create Connection: invalid uri argument value i.e. {uri}");
            return null;
        }

        if (developerId == null)
        {
            Log.Information($"Cannot Create Connection: null developer id argument");
            return null;
        }

        try
        {
            VssCredentials? credentials;
            try
            {
                credentials = developerId.GetCredentials();

                if (credentials == null)
                {
                    Log.Error($"Unable to get credentials for developerId");
                    return null;
                }
            }
            catch (MsalUiRequiredException ex)
            {
                Log.Error($"Unable to get credentials for developerId failed and requires user interaction {ex}");
                return null;
            }
            catch (MsalServiceException ex)
            {
                Log.Error($"Unable to get credentials for developerId: failed with MSAL service error: {ex}");
                return null;
            }
            catch (MsalClientException ex)
            {
                Log.Error($"Unable to get credentials for developerId: failed with MSAL client error: {ex}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to get credentials for developerId {ex}");
                return null;
            }

            var connection = new VssConnection(azureUri.Connection, credentials);
            if (connection != null)
            {
                Log.Debug($"Connection created for developer id");
                return connection;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed creating connection for developer id and {uri} with exception:", ex);
        }

        return null;
    }

    public static ConnectionResult CreateVssConnection(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Information($"Cannot Create Connection: invalid uri argument value i.e. {uri}");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidArgument, false);
        }

        if (developerId == null)
        {
            Log.Information($"Cannot Create Connection: invalid developer id argument");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidArgument, false);
        }

        VssCredentials? credentials;
        try
        {
            credentials = developerId.GetCredentials();
            if (credentials == null)
            {
                Log.Error($"Unable to get credentials for developerId");
                return new ConnectionResult(ResultType.Failure, ErrorType.InvalidDeveloperId, false);
            }
        }
        catch (MsalUiRequiredException ex)
        {
            Log.Error($"AcquireDeveloperAccountToken failed and requires user interaction {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.CredentialUIRequired, false, ex);
        }
        catch (MsalServiceException ex)
        {
            Log.Error($"AcquireDeveloperAccountToken failed with MSAL service error: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.MsalServiceError, false, ex);
        }
        catch (MsalClientException ex)
        {
            Log.Error($"AcquireDeveloperAccountToken failed with MSAL client error: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.MsalClientError, false, ex);
        }
        catch (Exception ex)
        {
            Log.Error($"AcquireDeveloperAccountToken failed with error: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.GenericCredentialFailure, true);
        }

        try
        {
            VssConnection? connection = null;
            if (credentials != null)
            {
                connection = new VssConnection(azureUri.Connection, credentials);
            }

            if (connection != null)
            {
                Log.Debug($"Created new connection to {azureUri.Connection} for {developerId.LoginId}");
                return new ConnectionResult(azureUri.Connection, null, connection);
            }
            else
            {
                Log.Error($"Connection to {azureUri.Connection} was null.");
                return new ConnectionResult(ResultType.Failure, ErrorType.NullConnection, false);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Unable to establish VssConnection: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.InitializeVssConnectionFailure, true, ex);
        }
    }

    /// <summary>
    /// Gets the Azure DevOps connection for the specified developer id.
    /// </summary>
    /// <param name="uri">The uri to an Azure DevOps resource.</param>
    /// <param name="developerId">The developer to authenticate with.</param>
    /// <returns>An authorized connection to the resource.</returns>
    /// <exception cref="ArgumentException">If the azure uri is not valid.</exception>
    /// <exception cref="ArgumentNullException">If developerId is null.</exception>
    /// <exception cref="AzureClientException">If a connection can't be made.</exception>
    public static VssConnection GetConnectionForLoggedInDeveloper(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Error($"Uri is an invalid Azure Uri: {uri}");
            throw new ArgumentException(uri.ToString());
        }

        if (developerId == null)
        {
            Log.Error($"No logged in developer for which connection needs to be retrieved");
            throw new ArgumentNullException(null);
        }

        var connection = CreateConnection(azureUri.Connection, developerId);
        if (connection == null)
        {
            Log.Error($"Failed creating connection for developer id");
            throw new AzureClientException($"Failed creating Vss connection: {azureUri.Connection} for {developerId.LoginId}");
        }

        return connection;
    }

    public static ConnectionResult GetVssConnectionForLoggedInDeveloper(Uri uri, DeveloperId.DeveloperId developerId)
    {
        if (developerId == null)
        {
            Log.Error($"No logged in developer for which connection needs to be retrieved");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidDeveloperId, false);
        }

        return CreateVssConnection(uri, developerId);
    }

    public static T? GetClient<T>(string uri, DeveloperId.DeveloperId developerId)
       where T : VssHttpClientBase
    {
        if (string.IsNullOrEmpty(uri))
        {
            Log.Information($"Cannot GetClient: invalid uri argument value i.e. {uri}");
            return null;
        }

        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Information($"Cannot GetClient as uri validation failed: value of uri {uri}");
            return null;
        }

        return GetClient<T>(azureUri.Connection, developerId);
    }

    public static T? GetClient<T>(Uri uri, DeveloperId.DeveloperId developerId)
       where T : VssHttpClientBase
    {
        var connection = GetConnectionForLoggedInDeveloper(uri, developerId);
        if (connection == null)
        {
            return null;
        }

        return connection.GetClient<T>();
    }

    public static ConnectionResult GetAzureDevOpsClient<T>(string uri, DeveloperId.DeveloperId developerId)
        where T : VssHttpClientBase
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Information($"Cannot GetClient as uri validation failed: value of uri {uri}");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidArgument, false);
        }

        return GetAzureDevOpsClient<T>(azureUri.Uri, developerId);
    }

    public static ConnectionResult GetAzureDevOpsClient<T>(Uri uri, DeveloperId.DeveloperId developerId)
       where T : VssHttpClientBase
    {
        var connectionResult = GetVssConnectionForLoggedInDeveloper(uri, developerId);
        if (connectionResult.Result == ResultType.Failure)
        {
            return connectionResult;
        }

        if (connectionResult.Connection != null)
        {
            return new ConnectionResult(uri, connectionResult.Connection.GetClient<T>(), connectionResult.Connection);
        }

        return new ConnectionResult(ResultType.Failure, ErrorType.FailedGettingClient, false);
    }
}
