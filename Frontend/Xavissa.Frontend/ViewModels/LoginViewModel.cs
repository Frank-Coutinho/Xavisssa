using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Security.Claims;
using System.Threading.Tasks;
using ReactiveUI;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class LoginViewModel : ReactiveObject
    {
        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                this.RaiseAndSetIfChanged(ref _username, value);
                ValidateUsername();
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set
            {
                this.RaiseAndSetIfChanged(ref _password, value);
                ValidatePassword();
            }
        }

        private string _usernameError = string.Empty;
        public string UsernameError
        {
            get => _usernameError;
            private set => this.RaiseAndSetIfChanged(ref _usernameError, value);
        }

        private string _passwordError = string.Empty;
        public string PasswordError
        {
            get => _passwordError;
            private set => this.RaiseAndSetIfChanged(ref _passwordError, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }

        // Navigation actions for different roles
        public Action? NavigateToMainView { get; set; }
        public Action? NavigateToManagementView { get; set; }
        public Action? NavigateAfterLogin { get; set; }

        private readonly HttpClient _httpClient;

        public LoginViewModel()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5087/"), // adjust if needed
            };

            LoginCommand = ReactiveCommand.CreateFromTask(ExecuteLoginAsync);

            LoginCommand.ThrownExceptions.Subscribe(ex =>
            {
                Console.WriteLine("❌ LoginCommand Exception:");
                Console.WriteLine(ex);
                PasswordError = "An unexpected error occurred. Check console for details.";
            });
        }

        private void ValidateUsername() =>
            UsernameError = string.IsNullOrWhiteSpace(Username)
                ? "Username is required"
                : string.Empty;

        private void ValidatePassword() =>
            PasswordError = string.IsNullOrWhiteSpace(Password)
                ? "Password is required"
                : string.Empty;

        private async Task ExecuteLoginAsync()
        {
            ValidateUsername();
            ValidatePassword();

            if (!string.IsNullOrEmpty(UsernameError) || !string.IsNullOrEmpty(PasswordError))
                return;

            try
            {
                var loginData = new { username = Username, password = Password };
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null)
                    {
                        var token = result.Token;

                        var handler = new JwtSecurityTokenHandler();
                        var jwt = handler.ReadJwtToken(token);

                        var usernameClaim = jwt
                            .Claims.FirstOrDefault(c =>
                                c.Type == JwtRegisteredClaimNames.UniqueName
                            )
                            ?.Value;
                        var roleClaim = jwt
                            .Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)
                            ?.Value;

                        AuthService.SetCredentials(
                            token,
                            new LoginResponse
                            {
                                username = usernameClaim ?? "Unknown",
                                role = roleClaim ?? "Clerk",
                                Token = token,
                            }
                        );

                        Console.WriteLine($"✅ Logged in as {usernameClaim} ({roleClaim})");

                        NavigateAfterLogin?.Invoke();
                    }
                }
                else
                {
                    PasswordError = "Invalid username or password.";
                }
            }
            catch (HttpRequestException)
            {
                PasswordError = "Unable to connect to the server.";
            }
            catch (Exception ex)
            {
                PasswordError = "An unexpected error occurred.";
                Console.WriteLine(ex);
            }
        }

        public class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public string username { get; set; } = string.Empty;
            public string? role { get; set; }
            public string claimTypesRole { get; set; } = string.Empty;
            public List<Claim> allClaims { get; set; } = new List<Claim>();

            public string DisplayName => username;
        }

        public class Claim
        {
            public string type { get; set; } = string.Empty;
            public string value { get; set; } = string.Empty;
        }
    }
}
