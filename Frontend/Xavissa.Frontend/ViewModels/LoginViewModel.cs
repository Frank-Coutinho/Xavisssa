using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using System;
using System.Net.Http;


namespace Xavissa.Frontend.ViewModels
{
    public class LoginViewModel : ReactiveObject
    {
        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set => this.RaiseAndSetIfChanged(ref _username, value);
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = ReactiveCommand.CreateFromTask(ExecuteLoginAsync);
        }

        private async Task ExecuteLoginAsync()
        {
            // Example: Replace with actual API call
            await Task.Delay(500);

            if (Username == "admin" && Password == "1234")
            {
                // TODO: Navigate to dashboard
            }
            else
            {
                // TODO: Show error message
            }
        }
    }
}
