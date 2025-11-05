using System;
using ReactiveUI;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.ViewModels
{
    public class AppViewModel : ReactiveObject
    {
        private ReactiveObject _currentView;
        public ReactiveObject CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public AppViewModel()
        {
            ShowLoginView();
        }

        private void ShowLoginView()
        {
            Console.WriteLine("Switching to LoginView...");
            var loginVm = new LoginViewModel();

            // Unified navigation callback after login
            loginVm.NavigateAfterLogin = () =>
            {
                var role = AuthService.CurrentUser?.role?.ToLowerInvariant();

                if (role == "admin" || role == "superuser")
                    ShowManagementView();
                else
                    ShowMainView();
            };
            Console.WriteLine(
                $"NAVIGATION DEBUG: CurrentUser.role = '{AuthService.CurrentUser?.role}'"
            );

            CurrentView = loginVm;
            Console.WriteLine("LoginView set as current");
        }

        private void ShowMainView()
        {
            Console.WriteLine("[AppViewModel] Navigating to MainView...");
            var mainVm = new MainViewModel();

            if (AuthService.CurrentUser != null)
            {
                Console.WriteLine(
                    $"[AppViewModel] Current user: {AuthService.CurrentUser.DisplayName}"
                );
                mainVm.CurrentUser = AuthService.CurrentUser.DisplayName;
            }
            else
            {
                Console.WriteLine("[AppViewModel] Warning: AuthService.CurrentUser is null");
            }

            mainVm.NavigateToLogin = ShowLoginView;

            mainVm.RefreshUser();
            CurrentView = mainVm;
            Console.WriteLine($"[AppViewModel] CurrentUser for MainView = {mainVm.CurrentUser}");
        }

        private void ShowManagementView()
        {
            var managementVm = new ManagementViewModel();

            // Hook logout back to login
            managementVm.NavigateToLogin = ShowLoginView;

            managementVm.NavigateToMain = ShowMainView;
            Console.WriteLine(
                $"[AppViewModel] NavigateToMain assigned: {(managementVm.NavigateToMain == null ? "NULL" : "OK")}"
            );

            CurrentView = managementVm;
        }
    }
}
