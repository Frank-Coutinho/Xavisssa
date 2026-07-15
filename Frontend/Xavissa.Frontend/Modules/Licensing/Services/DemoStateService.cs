using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class DemoStateService : IDemoStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDemoApiClient _api;

    public DemoStateService(IDemoApiClient api)
    {
        _api = api;
    }

    public DemoSessionState Current { get; private set; } = new();
    public bool IsDemoActive => Current.Status == DemoModeStatus.Active && Current.IsActive && !IsExpired;
    public bool IsExpired => Current.Status == DemoModeStatus.Expired || (Current.ExpiresAt != default && DateTime.UtcNow >= NormalizeUtc(Current.ExpiresAt));
    public int RemainingSeconds => Current.ExpiresAt == default ? 0 : Math.Max(0, (int)(NormalizeUtc(Current.ExpiresAt) - DateTime.UtcNow).TotalSeconds);

    public event Action<DemoSessionState>? StateChanged;

    public async Task<DemoSessionState> LoadAsync()
    {
        if (!File.Exists(StatePath))
        {
            Current = new DemoSessionState();
            Publish();
            return Current;
        }

        Current = JsonSerializer.Deserialize<DemoSessionState>(await File.ReadAllTextAsync(StatePath), JsonOptions) ?? new DemoSessionState();
        await CheckExpirationAsync();
        return Current;
    }

    public async Task StartAsync(StartDemoSessionResponse response, DeviceIdentityDto device)
    {
        Current = new DemoSessionState
        {
            DemoSessionId = response.DemoSessionId,
            TenantId = response.TenantId,
            TenantCode = response.TenantCode,
            TenantName = response.TenantName,
            LicenseId = response.LicenseId,
            StartedAt = NormalizeUtc(response.StartedAt == default ? DateTime.UtcNow : response.StartedAt),
            ExpiresAt = NormalizeUtc(response.ExpiresAt),
            LastActivityAt = DateTime.UtcNow,
            DeviceFingerprint = device.DeviceFingerprint,
            ResetOnClose = response.ResetOnClose,
            IsActive = true,
            Status = DemoModeStatus.Active,
        };

        Current.RemainingSeconds = RemainingSeconds;
        await PersistAsync();
        Publish();
        await TrackEventAsync("DemoStarted", description: "Demo session started.");
    }

    public async Task<DemoSessionState> CheckExpirationAsync()
    {
        if (Current.Status == DemoModeStatus.NotStarted)
            return Current;

        Current.RemainingSeconds = RemainingSeconds;
        if (Current.IsActive && Current.ExpiresAt != default && DateTime.UtcNow >= NormalizeUtc(Current.ExpiresAt))
            await MarkExpiredAsync("Demo session expired.");
        else
            Publish();

        return Current;
    }

    public async Task MarkExpiredAsync(string? description = null)
    {
        if (Current.Status == DemoModeStatus.NotStarted)
            return;

        Current.Status = DemoModeStatus.Expired;
        Current.IsActive = false;
        Current.RemainingSeconds = 0;
        Current.LastActivityAt = DateTime.UtcNow;
        await PersistAsync();
        Publish();
        await TrackEventAsync("DemoExpired", description: description ?? "Demo session expired.");
    }

    public async Task TrackEventAsync(string eventType, string? entityName = null, string? entityId = null, string? description = null)
    {
        if (Current.DemoSessionId == null || string.IsNullOrWhiteSpace(eventType))
            return;

        Current.LastActivityAt = DateTime.UtcNow;
        Current.RemainingSeconds = RemainingSeconds;
        await PersistAsync();
        Publish();

        try
        {
            await _api.TrackDemoEventAsync(new TrackDemoEventRequest
            {
                DemoSessionId = Current.DemoSessionId,
                EventType = eventType,
                EntityName = entityName,
                EntityId = entityId,
                Description = description,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Demo event tracking failed: " + ex.Message);
        }
    }

    public async Task ClearAsync()
    {
        Current = new DemoSessionState();
        if (File.Exists(StatePath))
            File.Delete(StatePath);

        await Task.CompletedTask;
        Publish();
    }

    private async Task PersistAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        await File.WriteAllTextAsync(StatePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    private void Publish()
    {
        Current.RemainingSeconds = RemainingSeconds;
        StateChanged?.Invoke(Current);
    }

    private static string StatePath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xavissa");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "demo-session-state.json");
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
