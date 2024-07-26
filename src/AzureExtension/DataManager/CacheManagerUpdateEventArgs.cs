// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataManager;

public delegate void CacheManagerUpdateEventHandler(object? source, CacheManagerUpdateEventArgs e);

public enum CacheManagerUpdateKind
{
    Started,
    Updated,
    Cleared,
    Error,
    Cancel,
    Account,
}

public class CacheManagerUpdateEventArgs : EventArgs
{
    private readonly CacheManagerUpdateKind _kind;
    private readonly Exception? _exception;

    public CacheManagerUpdateEventArgs(CacheManagerUpdateKind updateKind, Exception? exception = null)
    {
        _kind = updateKind;
        _exception = exception;
    }

    public CacheManagerUpdateKind Kind => _kind;

    public Exception? Exception => _exception;
}
