using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class AuthRepositoryOnline : IAuthRepositoryOnline
    {
        private readonly HttpClient _http;
        private readonly IApiErrorHandler _errors;

        public AuthRepositoryOnline(IHttpClientFactory factory, IApiErrorHandler errors)
        {
            _http = factory.CreateClient("backend");
            _errors = errors;
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            var body = new { username, password };

            Console.WriteLine($"[Auth] Attempting online login for '{username}'.");
            var response = await _http.PostAsJsonAsync("api/Auth/login", body);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Auth] Login failed for '{username}' with status {(int)response.StatusCode}.");
                await _errors.EnsureSuccessAsync(response, "Invalid credentials or no active assignment.");
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Console.WriteLine(
                $"[Auth] Login succeeded for '{username}'. Token issued: {!string.IsNullOrWhiteSpace(login?.Token)}. " +
                $"PlatformRoleCode={login?.PlatformRoleCode ?? login?.PlatformRole}, ActingRole={login?.ActingRole}, SelectedTenantId={login?.SelectedTenantId}, SelectedStoreId={login?.SelectedStoreId}.");
            return login;
        }

        public async Task<LoginResponse?> SelectStoreAsync(string username, string password, int storeId)
        {
            var body = new { username, password, storeId };

            Console.WriteLine($"[Auth] Requesting store-scoped token for '{username}' at store {storeId}.");
            var response = await _http.PostAsJsonAsync("api/Auth/select-store", body);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Auth] Store selection failed for '{username}' with status {(int)response.StatusCode}.");
                await _errors.EnsureSuccessAsync(response, "Invalid credentials or store not assigned.");
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Console.WriteLine(
                $"[Auth] Store selection succeeded for '{username}'. Token issued: {!string.IsNullOrWhiteSpace(login?.Token)}. " +
                $"PlatformRoleCode={login?.PlatformRoleCode ?? login?.PlatformRole}, ActingRole={login?.ActingRole}, SelectedTenantId={login?.SelectedTenantId}, SelectedStoreId={login?.SelectedStoreId}.");
            return login;
        }
    }
}
