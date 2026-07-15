using System;

namespace Xavissa.Frontend.Services
{
    public interface IBackgroundSyncService
    {
        event EventHandler<BackgroundSyncStatusChangedEventArgs>? StatusChanged;

        BackgroundSyncStatus CurrentStatus { get; }

        void RequestSync(BackgroundSyncReason reason);
    }

    public enum BackgroundSyncReason
    {
        Periodic,
        Reconnected,
        StoreChanged,
        SaleCompleted,
        Manual,
    }

    public enum BackgroundSyncState
    {
        Idle,
        Running,
        Skipped,
        Failed,
    }

    public sealed class BackgroundSyncStatus
    {
        public BackgroundSyncState State { get; init; } = BackgroundSyncState.Idle;
        public BackgroundSyncReason Reason { get; init; } = BackgroundSyncReason.Periodic;
        public string Message { get; init; } = string.Empty;
        public DateTimeOffset? LastSuccessfulSyncAt { get; init; }
        public bool ShouldRefreshLocalViews { get; init; }
        public Exception? Error { get; init; }
    }

    public sealed class BackgroundSyncStatusChangedEventArgs : EventArgs
    {
        public BackgroundSyncStatusChangedEventArgs(BackgroundSyncStatus status)
        {
            Status = status;
        }

        public BackgroundSyncStatus Status { get; }
    }
}
