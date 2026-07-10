using System;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IDemoStateService
{
    DemoSessionState Current { get; }
    bool IsDemoActive { get; }
    bool IsExpired { get; }
    int RemainingSeconds { get; }
    event Action<DemoSessionState>? StateChanged;
    Task<DemoSessionState> LoadAsync();
    Task StartAsync(StartDemoSessionResponse response, DeviceIdentityDto device);
    Task<DemoSessionState> CheckExpirationAsync();
    Task MarkExpiredAsync(string? description = null);
    Task TrackEventAsync(string eventType, string? entityName = null, string? entityId = null, string? description = null);
    Task ClearAsync();
}
