// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using DevHomeAzureExtension.Providers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHomeAzureExtension.DataManager;

public class CacheManager : IDisposable
{
    private static readonly string _cacheManagerLastUpdatedMetaDataKey = "CacheManagerLastUpdated";

    // Frequency the CacheManager checks for an update.
    private static readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan _defaultAccountUpdateFrequency = TimeSpan.FromDays(3);

    private static readonly object _instanceLock = new();

    private readonly object _stateLock = new();

    private static CacheManager? _singletonInstance;

    private readonly ILogger _log;

    private CancellationTokenSource _cancelSource;

    private IAzureDataManager DataManager { get; }

    public bool UpdateInProgress { get; private set; }

    public bool NeverUpdated => LastUpdated == DateTime.MinValue;

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
        CancelUpdateInProgress();

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
        if (DateTime.UtcNow - LastUpdateTime < _updateInterval)
        {
            return;
        }

        try
        {
            _log.Debug("Starting account data update.");
            await Update(_defaultAccountUpdateFrequency);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update failed unexpectedly.");
        }

        LastUpdateTime = DateTime.UtcNow;
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
        await DataManager.UpdateDataForAccountsAsync(olderThan ?? _defaultAccountUpdateFrequency, options, Id);
    }

    private void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        if (e.Requestor == Id)
        {
            Log.Debug("DataManager update received");
            switch (e.Kind)
            {
                case DataManagerUpdateKind.Account:
                    // Account is sent after each organization has been updated, but the entire cache
                    // is not necessarily updated. We will treat this as an update is still in progress, and
                    // notify others who may be waiting for an opportunity to query the database.
                    // Receiving this event means a transaction was just completed and the datastore is
                    // briefly unlocked for queries.
                    SendUpdateEvent(this, CacheManagerUpdateKind.Account);
                    break;

                case DataManagerUpdateKind.Cache:
                    lock (_stateLock)
                    {
                        UpdateInProgress = false;
                        _pendingRefresh = false;
                        _clearNextDataUpdate = false;
                        if (e.Context.AccountsUpdated > 0)
                        {
                            // We will update this anytime we update any organization data.
                            LastUpdated = DateTime.UtcNow;
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
                        // Error condition means we don't know what state we are in, but we want to clear everything so retries
                        // can be allowed.
                        UpdateInProgress = false;
                        _pendingRefresh = false;
                    }

                    _log.Error(e.Exception, "Update failed.");
                    SendUpdateEvent(this, CacheManagerUpdateKind.Error, e.Exception);
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
        try
        {
            // Use switch here in case new states get added later to ensure we handle all cases.
            switch (DeveloperIdProvider.GetInstance().GetDeveloperIdState(args))
            {
                case AuthenticationState.LoggedIn:
                    // New developer logging in could change some of the data, so we will do a refresh.
                    // This will cancel any sync in progress and start over.
                    _log.Information("New developer account logged in, refreshing data.");
                    _ = Refresh();
                    return;

                case AuthenticationState.LoggedOut:
                    // When a developer account logs out the datastore will be re-created on next startup.
                    // Any data sync we do here will be discarded on next startup, but we can try to make
                    // the remainder of this session consistent with the current set of developer ids.
                    _log.Information("Developer account logged in, refreshing data.");
                    _ = Refresh();
                    return;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed getting DeveloperId state while trying to handle DeveloperId change.");
        }
    }

    private DateTime GetLastUpdated()
    {
        var lastCacheUpdate = DataManager.GetMetaData(_cacheManagerLastUpdatedMetaDataKey);
        if (string.IsNullOrEmpty(lastCacheUpdate))
        {
            return DateTime.MinValue;
        }

        return lastCacheUpdate.ToDateTime();
    }

    private void SetLastUpdated(DateTime time)
    {
        DataManager?.SetMetaData(_cacheManagerLastUpdatedMetaDataKey, time.ToDataStoreString());
    }

    private bool _disposed; // To detect redundant calls.

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
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

            _disposed = true;
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
