using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuestPDF.Infrastructure;

namespace Xavissa.Frontend
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            try
            {
                AvaloniaXamlLoader.Load(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Failed to load App.axaml:");
                Console.WriteLine(ex);
            }
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = new MainWindow
                    {
                        Width = 1000,
                        Height = 600,
                        Content = new Views.AppView { DataContext = new ViewModels.AppViewModel() },
                    };
                }

                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error during OnFrameworkInitializationCompleted:");
                Console.WriteLine(ex);
            }
        }
    }
}
