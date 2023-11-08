// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

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
}
