// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataManager;

public delegate void DataManagerUpdateEventHandler(object? source, DataManagerUpdateEventArgs e);

public enum DataManagerUpdateKind
{
    Query,          // A custom query was updated, which could be any amount of data in the datastore.
    PullRequest,
    Error,
    Cache,
    Account,
    Cancel,
    Developer,
}

public class DataManagerUpdateEventArgs : EventArgs
{
    private readonly dynamic _context;
    private readonly DataManagerUpdateKind _kind;
    private readonly Guid _requestor;
    private readonly Exception? _exception;

    public DataManagerUpdateEventArgs(DataManagerUpdateKind updateKind, Guid requestor, dynamic context, Exception? exception = null)
    {
        _kind = updateKind;
        _requestor = requestor;
        _context = context;
        _exception = exception;
    }

    public DataManagerUpdateKind Kind => _kind;

    public dynamic Context => _context;

    public Guid Requestor => _requestor;

    public Exception? Exception => _exception;
}
