// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.Services.WebApi;

namespace DevHomeAzureExtension.Client;

public class ConnectionResult
{
    public ResultType Result { get; private set; } = ResultType.Unknown;

    public ErrorType Error { get; private set; } = ErrorType.Unknown;

    public Exception? Exception
    {
        get; private set;
    }

    public Uri? UriValue
    {
        get; private set;
    }

    public VssHttpClientBase? T
    {
        get; private set;
    }

    public VssConnection? Connection
    {
        get; private set;
    }

    public bool AttemptSilentReauthorization
    {
        get;
        set;
    }

    public ConnectionResult(Uri? uri, VssHttpClientBase? type, VssConnection? connection)
    {
        Result = ResultType.Success;
        Error = ErrorType.None;
        UriValue = uri;
        T = type;
        Connection = connection;
    }

    public ConnectionResult(ResultType result, ErrorType error, bool attemptSilentReauthorization, Exception? exception = null)
    {
        Result = result;
        Error = error;
        AttemptSilentReauthorization = attemptSilentReauthorization;
        Exception = exception;
    }
}
