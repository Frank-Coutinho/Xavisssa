using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Xavissa.Frontend.Services
{
    public class BackendProcessManager : IBackendProcessManager
    {
        private Process? _backend;

        public Task StartAsync()
        {
            string exe = GetBackendPath();

            if (!File.Exists(exe))
            {
                Console.WriteLine($"❌ Backend executable not found: {exe}");
                return Task.CompletedTask;
            }

            _backend = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                },
            };

            _backend.Start();
            Console.WriteLine("🚀 Backend started");

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            try
            {
                if (_backend != null && !_backend.HasExited)
                {
                    Console.WriteLine("🛑 Stopping backend...");
                    _backend.Kill(true);
                    _backend.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error stopping backend: " + ex.Message);
            }

            return Task.CompletedTask;
        }

        private string GetBackendPath()
        {
            string baseDir = AppContext.BaseDirectory;

            string publishPath = Path.Combine(baseDir, @"..\backend\Xavissa.Backend.exe");
            if (File.Exists(publishPath))
                return Path.GetFullPath(publishPath);

            string devPath = Path.Combine(
                baseDir,
                @"..\..\..\backend\Xavissa.Backend\bin\Release\net9.0\Xavissa.Backend.exe"
            );

            return Path.GetFullPath(devPath);
        }
    }
}
