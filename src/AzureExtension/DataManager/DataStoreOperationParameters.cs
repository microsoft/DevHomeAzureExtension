// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataModel;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension;

public class DataStoreOperationParameters
{
    public IEnumerable<AzureUri> Uris { get; set; } = new List<AzureUri>();

    public string OperationName { get; set; } = string.Empty;

    public string LoginId { get; set; } = string.Empty;

    public Guid Requestor { get; set; }

    public DeveloperId.DeveloperId? DeveloperId { get; set; }

    public PullRequestView PullRequestView { get; set; } = PullRequestView.Unknown;

    public RequestOptions? RequestOptions { get; set; }

    public DataStoreOperationParameters()
    {
    }

    public override string ToString() => $"{OperationName} UriCount: {Uris.Count()} - {LoginId}";
}
