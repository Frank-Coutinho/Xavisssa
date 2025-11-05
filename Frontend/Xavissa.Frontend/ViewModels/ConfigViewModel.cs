using System;
using System.Reactive;
using System.Timers;
using ReactiveUI;

namespace Xavissa.Frontend.ViewModels
{
    public class ConfigViewModel : ViewModelBase
    {
        private string _currentDateTime;
        public string CurrentDateTime
        {
            get => _currentDateTime;
            set => this.RaiseAndSetIfChanged(ref _currentDateTime, value);
        }

        // Example configurable settings
        private string _storeName = "Xavissa Store";
        public string StoreName
        {
            get => _storeName;
            set => this.RaiseAndSetIfChanged(ref _storeName, value);
        }

        private string _currency = "MZN";
        public string Currency
        {
            get => _currency;
            set => this.RaiseAndSetIfChanged(ref _currency, value);
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set => this.RaiseAndSetIfChanged(ref _isDarkMode, value);
        }

        public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }

        public ConfigViewModel()
        {
            // Create timer for updating date/time every second
            var timer = new Timer(1000);
            timer.Elapsed += (s, e) =>
            {
                CurrentDateTime = DateTime.Now.ToString("dd MMMM yyyy - HH:mm:ss");
            };
            timer.Start();

            // Command to "save" the settings
            SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);

            // Initialize the clock
            CurrentDateTime = DateTime.Now.ToString("dd MMMM yyyy - HH:mm:ss");
        }

        private void SaveSettings()
        {
            // Here you would normally persist to a file, database, etc.
            Console.WriteLine($"Settings saved: {StoreName}, {Currency}, DarkMode={IsDarkMode}");
        }
    }
}
