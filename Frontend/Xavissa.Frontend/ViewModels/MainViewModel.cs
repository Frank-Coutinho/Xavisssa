using System;
using System.Reactive;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;
        private string _currentUser;
        private string _currentDate;
        private string _currentTime;

        // Button background brushes
        private IBrush _homeButtonBackground;
        private IBrush _historyButtonBackground;
        private IBrush _configButtonBackground;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentView, value);
                UpdateButtonBackgrounds();
            }
        }

        public string CurrentUser
        {
            get => _currentUser;
            set => this.RaiseAndSetIfChanged(ref _currentUser, value);
        }

        public string CurrentDate
        {
            get => _currentDate;
            set => this.RaiseAndSetIfChanged(ref _currentDate, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => this.RaiseAndSetIfChanged(ref _currentTime, value);
        }

        // Button backgrounds bound to XAML
        public IBrush HomeButtonBackground
        {
            get => _homeButtonBackground;
            private set => this.RaiseAndSetIfChanged(ref _homeButtonBackground, value);
        }

        public IBrush HistoryButtonBackground
        {
            get => _historyButtonBackground;
            private set => this.RaiseAndSetIfChanged(ref _historyButtonBackground, value);
        }

        public IBrush ConfigButtonBackground
        {
            get => _configButtonBackground;
            private set => this.RaiseAndSetIfChanged(ref _configButtonBackground, value);
        }

        public void RefreshUser()
        {
            CurrentUser = AuthService.CurrentUser?.DisplayName ?? "Guest";
        }

        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
        public ReactiveCommand<Unit, ViewModelBase> ShowHomeCommand { get; }
        public ReactiveCommand<Unit, ViewModelBase> ShowHistoryCommand { get; }
        public ReactiveCommand<Unit, ViewModelBase> ShowConfigCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowMenuCommand { get; }
        public Action? NavigateToLogin { get; set; }

        public MainViewModel()
        {
            // Subscribe to AuthService changes
            AuthService.UserChanged += () =>
            {
                CurrentUser = AuthService.CurrentUser?.DisplayName ?? "Guest";
                Console.WriteLine($"[MainViewModel] CurrentUser updated: {CurrentUser}");
            };
            RefreshUser();

            CurrentUser = AuthService.CurrentUser?.DisplayName ?? "Guest";

            ShowHomeCommand = ReactiveCommand.Create(() => CurrentView = new HomeViewModel());
            ShowHistoryCommand = ReactiveCommand.Create(() => CurrentView = new HistoryViewModel());
            ShowConfigCommand = ReactiveCommand.Create(() => CurrentView = new ConfigViewModel());
            ShowMenuCommand = ReactiveCommand.Create(
                () => { /* Could toggle a side menu later */
                }
            );

            LogoutCommand = ReactiveCommand.Create(() =>
            {
                Console.WriteLine("LogoutCommand triggered");
                AuthService.Clear();
                Console.WriteLine(
                    $"Navigating to login... CurrentView before = {CurrentView?.GetType().Name}"
                );
                NavigateToLogin?.Invoke();
                this.RaisePropertyChanged(nameof(CurrentView));
                Console.WriteLine("NavigateToLogin invoked");
            });

            // Initialize
            CurrentView = new HomeViewModel();
            UpdateButtonBackgrounds();

            UpdateTime();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => UpdateTime();
            timer.Start();
        }

        private void UpdateButtonBackgrounds()
        {
            // Update button backgrounds based on current view
            HomeButtonBackground =
                CurrentView is HomeViewModel ? Brushes.DarkGreen : Brushes.LightGreen;
            HistoryButtonBackground =
                CurrentView is HistoryViewModel ? Brushes.DarkGreen : Brushes.LightGreen;
            ConfigButtonBackground =
                CurrentView is ConfigViewModel ? Brushes.DarkGreen : Brushes.LightGreen;
        }

        private void UpdateTime()
        {
            CurrentDate = DateTime.Now.ToString("dd MMM yyyy");
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
