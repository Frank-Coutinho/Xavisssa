using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.ViewModels
{
    public class ManagementViewModel : ReactiveObject
    {
        private readonly HttpClient _httpClient;

        // Existing properties and collections
        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public string NewProductName { get; set; } = string.Empty;
        public string NewProductColor { get; set; } = string.Empty;
        public decimal NewProductPrice { get; set; } = 0;
        public int NewProductQuantity { get; set; } = 0;

        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set => this.RaiseAndSetIfChanged(ref _selectedUser, value);
        }

        // New user form fields
        private string _newUsername = string.Empty;
        public string NewUsername
        {
            get => _newUsername;
            set => this.RaiseAndSetIfChanged(ref _newUsername, value);
        }

        private string _newEmail = string.Empty;
        public string NewEmail
        {
            get => _newEmail;
            set => this.RaiseAndSetIfChanged(ref _newEmail, value);
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set => this.RaiseAndSetIfChanged(ref _newPassword, value);
        }

        private UserRole _selectedRole = UserRole.Clerk;
        public UserRole SelectedRole
        {
            get => _selectedRole;
            set => this.RaiseAndSetIfChanged(ref _selectedRole, value);
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);

                // Debug: Check if this setter is called
                Console.WriteLine($"SelectedTabIndex changed: {value}");

                // Assuming Products tab is index 2 (0-based)
                if (value == 2)
                {
                    LoadProductsCommand.Execute().Subscribe();
                }
            }
        }

        public Array AvailableRoles => Enum.GetValues(typeof(UserRole));

        // Commands
        public ReactiveCommand<Unit, Unit> LoadUsersCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateUserCommand { get; }
        public ReactiveCommand<User, Unit> UpdateUserCommand { get; }
        public ReactiveCommand<User, Unit> DeleteUserCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToMainCommand { get; }
        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadProductsCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateProductCommand { get; }
        public ReactiveCommand<Product, Unit> UpdateProductCommand { get; }
        public ReactiveCommand<Product, Unit> DeleteProductCommand { get; }

        public event EventHandler<MessageEventArgs>? ShowMessageRequested;
        public event EventHandler<ConfirmDeleteEventArgs>? ConfirmDeleteRequested;

        // Navigation action
        public Action? NavigateToMain { get; set; }
        public Action? NavigateToLogin { get; set; }

        public ManagementViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5087") };

            if (AuthService.IsLoggedIn)
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer",
                        AuthService.Token
                    );
            }

            LoadUsersCommand = ReactiveCommand.CreateFromTask(LoadUsersAsync);
            CreateUserCommand = ReactiveCommand.CreateFromTask(CreateUserAsync);
            UpdateUserCommand = ReactiveCommand.CreateFromTask<User>(UpdateUserAsync);
            DeleteUserCommand = ReactiveCommand.CreateFromTask<User>(DeleteUserAsync);
            GoToMainCommand = ReactiveCommand.Create(() =>
            {
                Console.WriteLine("[ManagementViewModel] GoToMainCommand triggered");
                Console.WriteLine(
                    $"[ManagementViewModel] NavigateToMain is {(NavigateToMain == null ? "NULL" : "SET")}"
                );
                NavigateToMain?.Invoke();
                Console.WriteLine("[ManagementViewModel] NavigateToMain invoked");
            });
            LogoutCommand = ReactiveCommand.Create(() =>
            {
                AuthService.Clear();
                NavigateToLogin?.Invoke();
            });
            LoadProductsCommand = ReactiveCommand.CreateFromTask(LoadProductsAsync);
            CreateProductCommand = ReactiveCommand.CreateFromTask(CreateProductAsync);
            UpdateProductCommand = ReactiveCommand.CreateFromTask<Product>(UpdateProductAsync);
            DeleteProductCommand = ReactiveCommand.CreateFromTask<Product>(DeleteProductAsync);

            _ = LoadUsersAsync();
        }

        // ---------------- API CALLS ---------------- //

        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _httpClient.GetFromJsonAsync<User[]>("/api/Users/all");
                Users.Clear();
                if (users != null)
                {
                    foreach (var user in users)
                        Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading users: {ex.Message}");
            }
        }

        private async Task CreateUserAsync()
        {
            try
            {
                var request = new CreateUserRequest
                {
                    Username = NewUsername,
                    Email = NewEmail,
                    Password = NewPassword,
                    UserRole = SelectedRole,
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"/api/UserManagement/create-{SelectedRole.ToString().ToLower()}",
                    request
                );
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("User created successfully!");
                    await LoadUsersAsync();

                    NewUsername = NewEmail = NewPassword = string.Empty;
                    SelectedRole = UserRole.Clerk;
                }
                else
                {
                    Console.WriteLine($"Failed to create user: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating user: {ex.Message}");
            }
        }

        private async Task UpdateUserAsync(User user)
        {
            if (user == null)
                return;

            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/UserManagement/{user.Id}",
                    user
                );
                if (response.IsSuccessStatusCode)
                    await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user: {ex.Message}");
            }
        }

        public async Task DeleteUserAsync(User user)
        {
            if (user == null)
                return;

            try
            {
                var response = await _httpClient.DeleteAsync($"/api/UserManagement/{user.Id}");
                if (response.IsSuccessStatusCode)
                    Users.Remove(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting user: {ex.Message}");
            }
        }

        private async Task LoadProductsAsync()
        {
            Console.WriteLine("LoadProductsAsync triggered");
            try
            {
                var products = await _httpClient.GetFromJsonAsync<Product[]>("/api/Product");
                Products.Clear();
                if (products != null)
                {
                    foreach (var p in products)
                        Products.Add(p);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading products: {ex.Message}");
            }
        }

        private async Task CreateProductAsync()
        {
            try
            {
                var product = new Product
                {
                    Name = NewProductName,
                    Color = NewProductColor,
                    Price = NewProductPrice,
                    StockQuantity = NewProductQuantity,
                };

                var response = await _httpClient.PostAsJsonAsync("/api/Product", product);
                if (response.IsSuccessStatusCode)
                {
                    await LoadProductsAsync();
                    NewProductName = NewProductColor = string.Empty;
                    NewProductPrice = 0;
                    NewProductQuantity = 0;
                }
                ShowMessageRequested?.Invoke(
                    this,
                    new MessageEventArgs("Success", "Product created successfully!", "success")
                );
            }
            catch (Exception ex)
            {
                ShowMessageRequested?.Invoke(
                    this,
                    new MessageEventArgs("Error", ex.Message, "error")
                );
                Console.WriteLine($"Error creating product: {ex.Message}");
            }
        }

        public async Task UpdateProductAsync(Product product)
        {
            if (product == null)
                return;

            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"/api/Product/{product.Id}",
                    product
                );
                if (response.IsSuccessStatusCode)
                    await LoadProductsAsync();
                ShowMessageRequested?.Invoke(
                    this,
                    new MessageEventArgs(
                        "Updated",
                        $"Product '{product.Name}' updated successfully.",
                        "success"
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating product: {ex.Message}");
            }
        }

        public async Task DeleteProductAsync(Product product)
        {
            if (product == null)
                return;

            try
            {
                var response = await _httpClient.DeleteAsync($"/api/Product/{product.Id}");
                if (response.IsSuccessStatusCode)
                    Products.Remove(product);
                ShowMessageRequested?.Invoke(
                    this,
                    new MessageEventArgs(
                        "Deleted",
                        $"Product '{product.Name}' deleted successfully.",
                        "success"
                    )
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting product: {ex.Message}");
            }
        }

        public class MessageEventArgs : EventArgs
        {
            public string Title { get; }
            public string Message { get; }
            public string Type { get; } // success | error | info

            public MessageEventArgs(string title, string message, string type)
            {
                Title = title;
                Message = message;
                Type = type;
            }
        }

        public class ConfirmDeleteEventArgs : EventArgs
        {
            public string ItemName { get; }
            public Action OnConfirmed { get; }
            public Action<bool> OnResponse { get; set; } = _ => { };

            public ConfirmDeleteEventArgs(string itemName, Action onConfirmed)
            {
                ItemName = itemName;
                OnConfirmed = onConfirmed;
            }
        }
    }
}
