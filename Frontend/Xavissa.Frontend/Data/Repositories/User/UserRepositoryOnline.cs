using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Mappers;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Data.Repositories
{
    public class UserRepositoryOnline : IUserRepositoryOnline
    {
        private readonly IHttpClientFactory _factory;
        private HttpClient Client => _factory.CreateClient("backend");

        public UserRepositoryOnline(IHttpClientFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public async Task CreateAsync(CreateUserRequest req)
        {
            var endpoint = ResolveCreateEndpoint(req);
            var payload = new
            {
                username = req.Username,
                email = req.Email,
                password = req.Password,
                platformRoleId = req.PlatformRoleId,
                platformRoleCode = string.IsNullOrWhiteSpace(req.PlatformRoleCode)
                    ? AppRoles.NormalizeRoleCode(req.PlatformRole)
                    : req.PlatformRoleCode,
                assignedRoleId = req.AssignedRoleId,
                assignedRoleCode = string.IsNullOrWhiteSpace(req.AssignedRoleCode)
                    ? AppRoles.NormalizeRoleCode(req.AssignedRole)
                    : req.AssignedRoleCode,
                tenantId = req.TenantId,
                storeId = req.StoreId,
            };

            Console.WriteLine(
                $"[Users] CreateAsync -> {endpoint}. Username='{req.Username}', AssignedRole='{req.AssignedRole}', " +
                $"TenantId={req.TenantId?.ToString() ?? "null"}, StoreId={req.StoreId?.ToString() ?? "null"}.");
            var response = await Client.PostAsJsonAsync(endpoint, payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Users] CreateAsync failed with status {(int)response.StatusCode}. Body='{body}'.");
                throw new HttpRequestException($"Failed to create user ({(int)response.StatusCode}): {body}", null, response.StatusCode);
            }
        }

        public async Task<List<User>> FetchAllFromServerAsync()
        {
            var auth = Client.DefaultRequestHeaders.Authorization;
            Debug.WriteLine(auth == null ? "No Authorization header set" : $"Authorization header: {auth.Scheme} {auth.Parameter?.Substring(0, 20)}...");
            var dtos = await Client.GetFromJsonAsync<List<UserReadDto>>("api/Users/all") ?? new List<UserReadDto>();
            return dtos.Select(UserMapper.FromReadDto).ToList();
        }

        public Task DeleteAsync(int id) => DeleteInternalAsync(id);

        public Task UpdateStatusAsync(int id, bool isActive) => UpdateStatusInternalAsync(id, isActive);

        private async Task DeleteInternalAsync(int id)
        {
            var response = await Client.DeleteAsync($"api/UserManagement/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to delete user ({(int)response.StatusCode}): {body}", null, response.StatusCode);
            }
        }

        private async Task UpdateStatusInternalAsync(int id, bool isActive)
        {
            var response = await Client.PutAsJsonAsync($"api/UserManagement/{id}", new
            {
                isActive,
            });

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to update user ({(int)response.StatusCode}): {body}", null, response.StatusCode);
            }
        }

        private string ResolveCreateEndpoint(CreateUserRequest req)
        {
            var platformRole = string.IsNullOrWhiteSpace(req.PlatformRoleCode)
                ? AppRoles.NormalizeRoleCode(req.PlatformRole)
                : req.PlatformRoleCode;
            var assignedRole = string.IsNullOrWhiteSpace(req.AssignedRoleCode)
                ? AppRoles.NormalizeRoleCode(req.AssignedRole)
                : req.AssignedRoleCode;

            if (AppRoles.IsSystemAdmin(platformRole))
                return "api/UserManagement/create-system-admin";

            if (AppRoles.IsSupport(platformRole))
                return "api/UserManagement/create-support";

            if (AppRoles.IsTenantAdmin(assignedRole))
                return "api/UserManagement/create-tenant-admin";

            if (AppRoles.IsStoreManager(assignedRole))
                return "api/UserManagement/create-store-manager";

            return "api/UserManagement/create-clerk";
        }
    }
}
