// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Client;

public class InfoResult
{
    public ResultType Result { get; } = ResultType.Unknown;

    public ErrorType Error { get; } = ErrorType.Unknown;

    public string Name { get; } = string.Empty;

    public string Description { get; } = string.Empty;

    public string ErrorMessage { get; } = string.Empty;

    public AzureUri AzureUri { get; }

    public InfoType InfoType { get; } = InfoType.Unknown;

    public Exception? Exception { get; }

    public InfoResult(AzureUri azureUri, InfoType type, string name, string description)
    {
        AzureUri = azureUri;
        Name = name;
        Description = description;
        InfoType = type;
        Result = ResultType.Success;
        Error = ErrorType.None;
    }

    public InfoResult(AzureUri azureUri, InfoType type, ResultType result, ErrorType error, Exception? exception = null)
    {
        AzureUri = azureUri;
        InfoType = type;
        Result = result;
        Error = error;
        Exception = exception;
        if (exception is not null)
        {
            ErrorMessage = exception.Message;
        }
        else
        {
            ErrorMessage = Error.ToString();
        }
    }
}
