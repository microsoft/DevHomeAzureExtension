// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Diagnostics.Eventing.Reader;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace DevHomeAzureExtension.Client;

public class AzureClientProvider
{
    private static VssConnection? CreateConnection(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Logger()?.ReportInfo($"Cannot Create Connection: invalid uri argument value i.e. {uri}");
            return null;
        }

        if (developerId == null)
        {
            Log.Logger()?.ReportInfo($"Cannot Create Connection: null developer id argument");
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
                    Log.Logger()?.ReportError($"Unable to get credentials for developerId");
                    return null;
                }
            }
            catch (MsalUiRequiredException ex)
            {
                Log.Logger()?.ReportError($"Unable to get credentials for developerId failed and requires user interaction {ex}");
                return null;
            }
            catch (MsalServiceException ex)
            {
                Log.Logger()?.ReportError($"Unable to get credentials for developerId: failed with msal service error: {ex}");
                return null;
            }
            catch (MsalClientException ex)
            {
                Log.Logger()?.ReportError($"Unable to get credentials for developerId: failed with msal client error: {ex}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Unable to get credentials for developerId {ex}");
                return null;
            }

            var connection = new VssConnection(azureUri.Connection, credentials);
            if (connection != null)
            {
                Log.Logger()?.ReportInfo($"Connection created for developer id");
                return connection;
            }
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Failed creating connection for developer id and {uri} with exception:", ex);
        }

        return null;
    }

    public static ConnectionResult CreateVssConnection(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Logger()?.ReportInfo($"Cannot Create Connection: invalid uri argument value i.e. {uri}");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidArgument, false);
        }

        if (developerId == null)
        {
            Log.Logger()?.ReportInfo($"Cannot Create Connection: invalid developer id argument");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidArgument, false);
        }

        VssCredentials? credentials;
        try
        {
            credentials = developerId.GetCredentials();
            if (credentials == null)
            {
                Log.Logger()?.ReportError($"Unable to get credentials for developerId");
                return new ConnectionResult(ResultType.Failure, ErrorType.InvalidDeveloperId, false);
            }
        }
        catch (MsalUiRequiredException ex)
        {
            Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed and requires user interaction {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.CredentialUIRequired, false, ex);
        }
        catch (MsalServiceException ex)
        {
            Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with msal service error: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.MsalServiceError, false, ex);
        }
        catch (MsalClientException ex)
        {
            Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with msal client error: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.MsalClientError, false, ex);
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"AcquireDeveloperAccountToken failed with error: {ex}");
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
                Log.Logger()?.ReportInfo($"Created new connection to {azureUri.Connection} for {developerId.LoginId}");
                return new ConnectionResult(azureUri.Connection, null, connection);
            }
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Unable to establish VssConnection: {ex}");
            return new ConnectionResult(ResultType.Failure, ErrorType.InitializeVssConnectionFailure, true, ex);
        }

        return new ConnectionResult(ResultType.Failure, ErrorType.Unknown, false);
    }

    public static VssConnection GetConnectionForLoggedInDeveloper(Uri uri, DeveloperId.DeveloperId developerId)
    {
        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Logger()?.ReportError($"Uri is an invalid Azure Uri: {uri}");
            throw new ArgumentException(uri.ToString());
        }

        if (developerId == null)
        {
            Log.Logger()?.ReportError($"No logged in developer for which connection needs to be retrieved");
            throw new ArgumentNullException(null);
        }

        var connection = CreateConnection(azureUri.Connection, developerId);
        if (connection == null)
        {
            Log.Logger()?.ReportError($"Failed creating connection for developer id");
            throw new AzureClientException($"Failed creating Vss connection: {azureUri.Connection} for {developerId.LoginId}");
        }

        return connection;
    }

    public static ConnectionResult GetVssConnectionForLoggedInDeveloper(Uri uri, DeveloperId.DeveloperId developerId)
    {
        if (developerId == null)
        {
            Log.Logger()?.ReportError($"No logged in developer for which connection needs to be retrieved");
            return new ConnectionResult(ResultType.Failure, ErrorType.InvalidDeveloperId, false);
        }

        return CreateVssConnection(uri, developerId);
    }

    public static T? GetClient<T>(string uri, DeveloperId.DeveloperId developerId)
       where T : VssHttpClientBase
    {
        if (string.IsNullOrEmpty(uri))
        {
            Log.Logger()?.ReportInfo($"Cannot GetClient: invalid uri argument value i.e. {uri}");
            return null;
        }

        var azureUri = new AzureUri(uri);
        if (!azureUri.IsValid)
        {
            Log.Logger()?.ReportInfo($"Cannot GetClient as uri validation failed: value of uri {uri}");
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
            Log.Logger()?.ReportInfo($"Cannot GetClient as uri validation failed: value of uri {uri}");
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
