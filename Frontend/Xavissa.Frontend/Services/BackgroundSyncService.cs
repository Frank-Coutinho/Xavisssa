using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xavissa.Frontend.Auth.Common;

namespace Xavissa.Frontend.Services
{
    public sealed class BackgroundSyncService : BackgroundService, IBackgroundSyncService
    {
        private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(3);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundSyncService> _logger;
        private readonly Channel<BackgroundSyncReason> _requests =
            Channel.CreateUnbounded<BackgroundSyncReason>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        private BackgroundSyncStatus _currentStatus = new();

        public BackgroundSyncService(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public event EventHandler<BackgroundSyncStatusChangedEventArgs>? StatusChanged;

        public BackgroundSyncStatus CurrentStatus => _currentStatus;

        public void RequestSync(BackgroundSyncReason reason)
        {
            if (!_requests.Writer.TryWrite(reason))
                _logger.LogWarning("Background sync request {Reason} could not be queued.", reason);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Frontend background sync service started.");

            var requestLoop = ProcessRequestsAsync(stoppingToken);
            var timerLoop = ProcessPeriodicAsync(stoppingToken);

            await Task.WhenAll(requestLoop, timerLoop);
        }

        private async Task ProcessRequestsAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var reason in _requests.Reader.ReadAllAsync(stoppingToken))
                    await RunSyncAsync(reason, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Frontend background sync request loop stopped.");
            }
        }

        private async Task ProcessPeriodicAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PeriodicInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunSyncAsync(BackgroundSyncReason.Periodic, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Frontend background sync timer stopped.");
            }
        }

        private async Task RunSyncAsync(BackgroundSyncReason reason, CancellationToken cancellationToken)
        {
            if (!await _syncLock.WaitAsync(0, cancellationToken))
            {
                Publish(new BackgroundSyncStatus
                {
                    State = BackgroundSyncState.Skipped,
                    Reason = reason,
                    Message = "A sync is already running.",
                    LastSuccessfulSyncAt = _currentStatus.LastSuccessfulSyncAt,
                });
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var scope = _scopeFactory.CreateScope();
                var net = scope.ServiceProvider.GetRequiredService<IConnectivityService>();
                var tokens = scope.ServiceProvider.GetRequiredService<IApiTokenProvider>();
                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var sync = scope.ServiceProvider.GetRequiredService<ISyncService>();

                if (!net.IsOnline() || string.IsNullOrWhiteSpace(tokens.Token))
                {
                    Publish(new BackgroundSyncStatus
                    {
                        State = BackgroundSyncState.Skipped,
                        Reason = reason,
                        Message = "Offline or unauthenticated; local SQLite remains the active data source.",
                        LastSuccessfulSyncAt = _currentStatus.LastSuccessfulSyncAt,
                    });
                    return;
                }

                Publish(new BackgroundSyncStatus
                {
                    State = BackgroundSyncState.Running,
                    Reason = reason,
                    Message = GetRunningMessage(reason),
                    LastSuccessfulSyncAt = _currentStatus.LastSuccessfulSyncAt,
                });

                await ExecuteReasonAsync(sync, auth, reason);

                Publish(new BackgroundSyncStatus
                {
                    State = BackgroundSyncState.Idle,
                    Reason = reason,
                    Message = "Sync complete.",
                    LastSuccessfulSyncAt = DateTimeOffset.Now,
                    ShouldRefreshLocalViews = reason != BackgroundSyncReason.Periodic,
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frontend background sync failed for reason {Reason}.", reason);
                Publish(new BackgroundSyncStatus
                {
                    State = BackgroundSyncState.Failed,
                    Reason = reason,
                    Message = "Sync failed. Local changes are still saved and will retry later.",
                    LastSuccessfulSyncAt = _currentStatus.LastSuccessfulSyncAt,
                    Error = ex,
                });
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private static Task ExecuteReasonAsync(
            ISyncService sync,
            IAuthService auth,
            BackgroundSyncReason reason)
        {
            return reason switch
            {
                BackgroundSyncReason.Reconnected => sync.SyncAfterReconnectAsync(),
                BackgroundSyncReason.StoreChanged => sync.SyncStoreScopedDataAsync(replaceStoreScopedProductCache: true),
                BackgroundSyncReason.SaleCompleted => auth.SelectedStoreId.HasValue
                    ? sync.SyncAfterSaleAsync(auth.SelectedStoreId.Value)
                    : sync.SyncAfterSaleAsync(),
                BackgroundSyncReason.Manual => sync.SyncAllAsync(),
                _ => sync.RefreshOperationalDataAsync(),
            };
        }

        private static string GetRunningMessage(BackgroundSyncReason reason)
        {
            return reason switch
            {
                BackgroundSyncReason.Reconnected => "Syncing after reconnect.",
                BackgroundSyncReason.StoreChanged => "Refreshing store-scoped data.",
                BackgroundSyncReason.SaleCompleted => "Syncing sale and stock changes.",
                BackgroundSyncReason.Manual => "Running manual sync.",
                _ => "Refreshing operational data.",
            };
        }

        private void Publish(BackgroundSyncStatus status)
        {
            _currentStatus = status;
            StatusChanged?.Invoke(this, new BackgroundSyncStatusChangedEventArgs(status));
        }
    }
}
