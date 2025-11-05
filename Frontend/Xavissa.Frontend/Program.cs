using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Xavissa.Frontend
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Application failed to start:");
                Console.WriteLine(ex);
                Console.ReadLine(); // Wait so you can see the error
            }
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().UseReactiveUI();
    }
}
