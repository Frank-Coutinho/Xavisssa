using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Xavissa.Frontend.Services;

public enum BackendReadinessState
{
    Starting,
    Ready,
    Unavailable,
    OfflineCachedMode,
}

public sealed class BackendHealthSnapshot
{
    public BackendReadinessState State { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsReady => State == BackendReadinessState.Ready;
}

public interface IBackendHealthService
{
    BackendHealthSnapshot Current { get; }
    event Action<BackendHealthSnapshot>? Changed;
    Task<BackendHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default);
    void StartMonitoring();
    void MarkBackendStarting();
    void MarkOfflineCachedMode(string? message = null);
}

public sealed class BackendHealthService : IBackendHealthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _gate = new();
    private DispatcherTimer? _timer;
    private BackendHealthSnapshot _current = new()
    {
        State = BackendReadinessState.Starting,
        Message = "Backend is starting...",
    };

    public BackendHealthService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public BackendHealthSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public event Action<BackendHealthSnapshot>? Changed;

    public void StartMonitoring()
    {
        if (_timer != null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_timer != null)
                return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timer.Tick += async (_, _) =>
            {
                try
                {
                    await CheckAsync();
                }
                catch
                {
                }
            };
            _timer.Start();
        });

        _ = CheckAsync();
    }

    public void MarkBackendStarting() =>
        Update(new BackendHealthSnapshot
        {
            State = BackendReadinessState.Starting,
            Message = "Backend is starting...",
        });

    public void MarkOfflineCachedMode(string? message = null) =>
        Update(new BackendHealthSnapshot
        {
            State = BackendReadinessState.OfflineCachedMode,
            Message = string.IsNullOrWhiteSpace(message)
                ? "Backend is unavailable. Cached/offline mode is available after a successful online login."
                : message,
        });

    public async Task<BackendHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            var client = _httpClientFactory.CreateClient("backend");
            using var request = new HttpRequestMessage(HttpMethod.Get, "health/connectivity");
            using var response = await client.SendAsync(request, timeout.Token);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var body = await response.Content.ReadFromJsonAsync<ConnectivityHealthDto>(cancellationToken: timeout.Token);
                return Update(new BackendHealthSnapshot
                {
                    State = BackendReadinessState.Ready,
                    Message = string.IsNullOrWhiteSpace(body?.Message) ? "Backend is ready." : body.Message,
                });
            }

            return Update(new BackendHealthSnapshot
            {
                State = BackendReadinessState.Unavailable,
                Message = $"Backend is unavailable ({(int)response.StatusCode}). Cached/offline mode may be used.",
            });
        }
        catch
        {
            return Update(new BackendHealthSnapshot
            {
                State = BackendReadinessState.Unavailable,
                Message = "Backend is unavailable. Cached/offline mode may be used.",
            });
        }
    }

    private BackendHealthSnapshot Update(BackendHealthSnapshot snapshot)
    {
        lock (_gate)
            _current = snapshot;

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    private sealed class ConnectivityHealthDto
    {
        public bool Online { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
