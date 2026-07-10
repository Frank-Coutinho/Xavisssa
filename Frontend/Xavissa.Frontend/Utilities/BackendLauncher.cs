using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Utilities
{
    public static class BackendLauncher
    {
        private static Process? _backendProcess;

        /// <summary>
        /// Starts the backend if not already running and waits until it's ready.
        /// </summary>
        /// <param name="backendExePath">Path to Xavissa.Backend.exe</param>
        /// <param name="backendUrl">Base URL to check if backend is ready (e.g., http://localhost:5000/api/health)</param>
        /// <param name="timeoutSeconds">Timeout in seconds to wait for backend</param>
        public static async Task StartBackendAsync(
            string backendExePath,
            string backendUrl = "http://localhost:5000",
            int timeoutSeconds = 10
        )
        {
            if (!File.Exists(backendExePath))
                throw new FileNotFoundException("Backend executable not found.", backendExePath);

            // Check if backend is already running by sending a HTTP GET
            if (!await IsBackendRunningAsync(backendUrl))
            {
                _backendProcess = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = backendExePath,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                    }
                );

                // Wait for backend to respond
                var start = DateTime.UtcNow;
                while (!await IsBackendRunningAsync(backendUrl))
                {
                    if ((DateTime.UtcNow - start).TotalSeconds > timeoutSeconds)
                        throw new TimeoutException(
                            "Backend did not start within the timeout period."
                        );

                    await Task.Delay(500);
                }
            }
        }

        private static async Task<bool> IsBackendRunningAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(500);
                var response = await client.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Optionally terminate the backend when frontend closes
        /// </summary>
        public static void StopBackend()
        {
            try
            {
                if (_backendProcess != null && !_backendProcess.HasExited)
                    _backendProcess.Kill();
            }
            catch { }
        }
    }
}
