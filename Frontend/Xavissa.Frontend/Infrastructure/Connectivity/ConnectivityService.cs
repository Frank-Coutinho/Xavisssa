using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Options;

namespace Xavissa.Frontend.Services
{
    public class ConnectivityService : IConnectivityService
    {
        private readonly IBackendHealthService? _health;
        private readonly HttpClient _client;
        private readonly object _gate = new();
        private DateTime _lastCheckedUtc = DateTime.MinValue;
        private bool _lastKnownStatus;

        public ConnectivityService(IOptions<BackendOptions> options, IBackendHealthService? health = null)
        {
            _health = health;
            _client = new HttpClient
            {
                BaseAddress = options.Value.BaseUri,
                Timeout = TimeSpan.FromSeconds(5),
            };
        }

        public bool IsOnline()
        {
            if (_health?.Current.IsReady == true)
                return true;

            lock (_gate)
            {
                if (DateTime.UtcNow - _lastCheckedUtc < TimeSpan.FromSeconds(3))
                    return _lastKnownStatus;
            }

            var isOnline = CheckConnectivity();

            lock (_gate)
            {
                _lastKnownStatus = isOnline;
                _lastCheckedUtc = DateTime.UtcNow;
            }

            return isOnline;
        }

        private bool CheckConnectivity()
        {
            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "health/connectivity"
                );

                var task = _client.SendAsync(request, CancellationToken.None);
                task.Wait();

                return task.Result.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
