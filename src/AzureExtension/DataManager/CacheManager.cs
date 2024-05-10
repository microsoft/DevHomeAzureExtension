// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Providers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension.DataManager;

public class CacheManager : IDisposable
{
    private static readonly string CacheManagerLastUpdatedMetaDataKey = "CacheManagerLastUpdated";

    // Frequency the CacheManager checks for an update.
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(4);

    private static readonly TimeSpan DefaultAccountUpdateFrequency = TimeSpan.FromDays(3);

    private static readonly object _instanceLock = new();

    private readonly object _stateLock = new();

    private static CacheManager? _singletonInstance;

    private readonly ILogger _log;

    private CancellationTokenSource _cancelSource;

    private IAzureDataManager DataManager { get; }

    public bool UpdateInProgress { get; private set; }

    public DateTime LastUpdated
    {
        get => GetLastUpdated();
        private set => SetLastUpdated(value);
    }

    // If the next update should clear sync data and force an update.
    private bool _clearNextDataUpdate;

    // If a Refresh call is pending and has not yet completed.
    private bool _pendingRefresh;

    public event CacheManagerUpdateEventHandler? OnUpdate;

    private DataUpdater DataUpdater { get; set; }

    private Guid Id { get; }

    private DateTime LastUpdateTime { get; set; } = DateTime.MinValue;

    public static CacheManager GetInstance()
    {
        try
        {
            lock (_instanceLock)
            {
                _singletonInstance ??= new CacheManager();
            }

            return _singletonInstance;
        }
        catch (Exception e)
        {
            var log = Log.ForContext("SourceContext", nameof(CacheManager));
            log.Error(e, $"Failed creating CacheManager.");
            throw;
        }
    }

    private CacheManager()
    {
        Id = Guid.NewGuid();
        _log = Log.ForContext("SourceContext", $"{nameof(CacheManager)}");
        DataManager = AzureDataManager.CreateInstance("CacheManager") ?? throw new DataStoreInaccessibleException();
        DataUpdater = new DataUpdater(PeriodicUpdate);
        AzureDataManager.OnUpdate += HandleDataManagerUpdate;
        DeveloperIdProvider.GetInstance().Changed += HandleDeveloperIdChange;
        _cancelSource = new CancellationTokenSource();
    }

    public void Start()
    {
        _log.Debug("Starting updater.");
        _ = DataUpdater.Start();
    }

    public void Stop()
    {
        DataUpdater.Stop();
    }

    public void CancelUpdateInProgress()
    {
        lock (_stateLock)
        {
            if (UpdateInProgress && !_cancelSource.IsCancellationRequested)
            {
                _log.Information("Cancelling update.");
                _cancelSource.Cancel();
            }
        }
    }

    public async Task Refresh()
    {
        lock (_stateLock)
        {
            if (_pendingRefresh)
            {
                _log.Debug("Refresh already pending, ignoring refresh request.");
                return;
            }

            _pendingRefresh = true;
            _clearNextDataUpdate = true;
        }

        CancelUpdateInProgress();
        await Update(TimeSpan.MinValue);
    }

    public IEnumerable<IRepository> GetRepositories()
    {
        var devHomeRepositories = new List<IRepository>();
        var repositories = DataManager.GetRepositories();
        foreach (var repository in repositories)
        {
            // Convert data model repositories, which have datastore connections
            // and table lookups as part of the data model, to a static snapshot
            // of the repository data. This will be sent to DevHome, so this is
            // needs to be detached data from our data store.
            devHomeRepositories.Add(new DevHomeRepository(repository));
        }

        return devHomeRepositories;
    }

    private async Task PeriodicUpdate()
    {
        // Only update per the update interval.
        // This is intended to be dynamic in the future.
        if (DateTime.Now - LastUpdateTime < UpdateInterval)
        {
            return;
        }

        try
        {
            _log.Debug("Starting account data update.");
            await Update(DefaultAccountUpdateFrequency);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update failed unexpectedly.");
        }

        LastUpdateTime = DateTime.Now;
        return;
    }

    private async Task Update(TimeSpan? olderThan)
    {
        var options = new RequestOptions();

        lock (_stateLock)
        {
            if (UpdateInProgress)
            {
                _log.Information("Update is in progress, ignoring request.");
                return;
            }

            UpdateInProgress = true;
            _cancelSource = new CancellationTokenSource();
            options.CancellationToken = _cancelSource.Token;
            options.Refresh = _clearNextDataUpdate;
        }

        _log.Debug("Starting account data update.");
        SendUpdateEvent(this, CacheManagerUpdateKind.Started);
        await DataManager.UpdateDataForAccountsAsync(olderThan ?? DefaultAccountUpdateFrequency, options, Id);
    }

    private void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Requestor == Id)
        {
            Log.Debug("DataManager update received");
            switch (e.Kind)
            {
                case DataManagerUpdateKind.Cache:
                    lock (_stateLock)
                    {
                        UpdateInProgress = false;
                        _pendingRefresh = false;
                        _clearNextDataUpdate = false;
                        if (e.Context.AccountsUpdated > 0)
                        {
                            // We will update this anytime we update any organization data.
                            LastUpdated = DateTime.Now;
                        }
                    }

                    _log.Information($"Accounts updated: {e.Context.AccountsUpdated}  Skipped: {e.Context.AccountsSkipped}  Errors: {e.Context.Errors}  Elapsed: {e.Context.TimeElapsed}");
                    if (e.Exception is not null)
                    {
                        _log.Warning($"First Error: {e.Exception.Message}");
                    }

                    SendUpdateEvent(this, CacheManagerUpdateKind.Updated);
                    break;

                case DataManagerUpdateKind.Cancel:
                    lock (_stateLock)
                    {
                        UpdateInProgress = false;
                    }

                    _log.Information($"Operation was cancelled. Updated: {e.Context.AccountsUpdated}  Skipped: {e.Context.AccountsSkipped}  Errors: {e.Context.Errors}  Elapsed: {e.Context.TimeElapsed}");
                    SendUpdateEvent(this, CacheManagerUpdateKind.Cancel);
                    if (_pendingRefresh)
                    {
                        // If we were pending a refresh it was likely because a refresh caused this
                        // cancellation, and there is a race between the update canncellation happening
                        // and the subsequent update. If we get here it means that we possibly need to update.
                        // If the Refresh call update executes first then this one will be ignored due
                        // to update in progress.
                        _ = Update(TimeSpan.MinValue);
                    }

                    break;

                case DataManagerUpdateKind.Error:
                    lock (_stateLock)
                    {
                        // Error condition means we dont' know what state we are in, but we want to clear everything so retries
                        // can be allowed.
                        UpdateInProgress = false;
                        _pendingRefresh = false;
                    }

                    _log.Error(e.Exception, "Update failed.");
                    SendUpdateEvent(this, CacheManagerUpdateKind.Error, e.Exception);
                    break;

                default:
                    break;
            }
        }
    }

    private void SendUpdateEvent(object? source, CacheManagerUpdateKind kind, Exception? ex = null)
    {
        if (OnUpdate != null)
        {
            _log.Debug($"Sending Update Event.  Kind: {kind}");
            OnUpdate.Invoke(source, new CacheManagerUpdateEventArgs(kind, ex));
        }
    }

    private void HandleDeveloperIdChange(IDeveloperIdProvider sender, IDeveloperId args)
    {
        if (DeveloperIdProvider.GetInstance().GetDeveloperIdState(args) == AuthenticationState.LoggedIn)
        {
            // New login means we trigger a new sync.
            _log.Information("New developer account logged in, syncing data...");
        }
    }

    private DateTime GetLastUpdated()
    {
        var lastCacheUpdate = DataManager.GetMetaData(CacheManagerLastUpdatedMetaDataKey);
        if (string.IsNullOrEmpty(lastCacheUpdate))
        {
            return DateTime.MinValue;
        }

        return lastCacheUpdate.ToDateTime();
    }

    private void SetLastUpdated(DateTime time)
    {
        DataManager?.SetMetaData(CacheManagerLastUpdatedMetaDataKey, time.ToDataStoreString());
    }

    private bool disposed; // To detect redundant calls.

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            _log.Debug("Disposing of all cacheManager resources.");

            if (disposing)
            {
                try
                {
                    _log.Debug("Disposing of all CacheManager resources.");
                    DataUpdater.Dispose();
                    DataManager.Dispose();
                    _cancelSource.Dispose();
                    AzureDataManager.OnUpdate -= HandleDataManagerUpdate;
                    DeveloperIdProvider.GetInstance().Changed -= HandleDeveloperIdChange;
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failed disposing");
                }
            }

            disposed = true;
        }
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
