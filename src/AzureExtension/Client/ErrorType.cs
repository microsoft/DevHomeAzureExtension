// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Client;

public enum ErrorType
{
    None,
    Unknown,
    InvalidArgument,
    EmptyUri,
    UriInvalidQuery,
    UriInvalidRepository,
    InvalidUri,
    NullDeveloperId,
    InvalidDeveloperId,
    QueryFailed,
    RepositoryFailed,
    FailedGettingClient,
    CredentialUIRequired,
    MsalServiceError,
    MsalClientError,
    GenericCredentialFailure,
    InitializeVssConnectionFailure,
    NullConnection,
    VssResourceNotFound,
}
