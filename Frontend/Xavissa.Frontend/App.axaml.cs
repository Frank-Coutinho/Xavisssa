using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using Xavissa.Frontend.Bootstrap.DependencyInjection;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend
{
    public partial class App : Application
    {
        private IHost? _host;

        public override void Initialize()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter(
                        "Microsoft.EntityFrameworkCore.Database.Command",
                        LogLevel.Warning
                    );
                })
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.AddXavissaFrontend(context.Configuration);
                    }
                )
                .Build();

            await _host.StartAsync();

            var printerService = _host.Services.GetRequiredService<IPrinterService>();
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            var localizationService = _host.Services.GetRequiredService<ILocalizationService>();

            localizationService.SetLanguage(printerService.LanguageCode);
            if (printerService.IsDarkMode)
                themeService.SetDark();
            else
                themeService.SetLight();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var rootVm = _host.Services.GetRequiredService<AppViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    Width = 1000,
                    Height = 600,
                    DataContext = rootVm,
                };

                desktop.Exit += async (_, __) =>
                {
                    var demoCleanup = _host.Services.GetRequiredService<IDemoCleanupService>();
                    await demoCleanup.CleanupOnCloseAsync();
                    var backend = _host.Services.GetRequiredService<IBackendProcessManager>();
                    await backend.StopAsync();
                    await _host.StopAsync();
                    _host.Dispose();
                };
            }

            // Keep the critical UI path short. Login also calls EnsureLocalSchemaAsync,
            // so this background warm-up is safe if the user acts immediately.
            _ = Task.Run(async () =>
            {
                using var scope = _host.Services.CreateScope();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
                    await db.EnsureLocalSchemaAsync();
                    Debug.WriteLine("Local SQLite schema ensured.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Local DB bootstrap failed:");
                    Debug.WriteLine(ex);
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var health = _host.Services.GetRequiredService<IBackendHealthService>();
                    health.MarkBackendStarting();
                    var backend = _host.Services.GetRequiredService<IBackendProcessManager>();
                    await backend.StartAsync();
                    health.StartMonitoring();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Backend startup failed: " + ex.Message);
                    _host.Services.GetRequiredService<IBackendHealthService>().MarkOfflineCachedMode();
                }
            });

            base.OnFrameworkInitializationCompleted();
        }
    }
}
