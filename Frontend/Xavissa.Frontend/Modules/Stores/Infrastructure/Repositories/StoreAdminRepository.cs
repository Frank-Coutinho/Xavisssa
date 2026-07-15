using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.Auth.Common;

namespace Xavissa.Frontend.Data.Repositories
{
    public class StoreAdminRepository : IStoreAdminRepository
    {
        private readonly HttpClient _client;
        private readonly IConnectivityService _net;
        private readonly IApiTokenProvider _tokens;
        private readonly IAuthService _auth;
        private readonly IApiErrorHandler _errors;
        private readonly IDbContextFactory<LocalDbContext> _factory;
        private readonly IDemoStateService _demoState;

        public StoreAdminRepository(
            IHttpClientFactory factory,
            IDbContextFactory<LocalDbContext> dbFactory,
            IConnectivityService net,
            IApiTokenProvider tokens,
            IAuthService auth,
            IApiErrorHandler errors,
            IDemoStateService demoState)
        {
            _client = factory.CreateClient("backend");
            _factory = dbFactory;
            _net = net;
            _tokens = tokens;
            _auth = auth;
            _errors = errors;
            _demoState = demoState;
        }

        public async Task<List<StoreRecord>> GetStoresAsync()
        {
            if (!CanUseAuthenticatedOnline())
                return await GetLocalStoresAsync();

            try
            {
                var stores = await _client.GetFromJsonAsync<List<StoreRecord>>("api/Stores") ?? new List<StoreRecord>();
                await ReplaceLocalStoresAsync(stores);
                return stores;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"Stores sync denied ({(int?)ex.StatusCode}). Falling back to cached auth stores.");
                return await GetLocalStoresAsync();
            }

            return await GetLocalStoresAsync();
        }

        public async Task<StoreRecord> CreateStoreAsync(StoreRecord store)
        {
            await EnsureDemoCanWriteAsync();
            Console.WriteLine(
                $"[Stores] CreateStore requested. TenantId={store.TenantId}, Name='{store.Name}', " +
                $"HasToken={CanUseAuthenticatedOnline()}.");
            var response = await _client.PostAsJsonAsync("api/Stores", store);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Stores] CreateStore failed with status {(int)response.StatusCode}. Body='{body}'.");
            }
            await _errors.EnsureSuccessAsync(response, "The store could not be created.");
            var created = await response.Content.ReadFromJsonAsync<StoreRecord>() ?? throw new Exception("Store response was empty.");
            await UpsertLocalStoreAsync(created);
            return created;
        }

        public async Task<StoreRecord> UpdateStoreAsync(StoreRecord store)
        {
            await EnsureDemoCanWriteAsync();
            var response = await _client.PutAsJsonAsync($"api/Stores/{store.Id}", store);
            response.EnsureSuccessStatusCode();
            var updated = await response.Content.ReadFromJsonAsync<StoreRecord>() ?? throw new Exception("Store response was empty.");
            await UpsertLocalStoreAsync(updated);
            return updated;
        }

        public Task<StoreRecord> DeactivateStoreAsync(StoreRecord store)
        {
            return UpdateStoreAsync(new StoreRecord
            {
                Id = store.Id,
                TenantId = store.TenantId,
                Name = store.Name,
                Code = store.Code,
                IsActive = false,
            });
        }

        public async Task<bool> CanDeleteStoreAsync(int storeId)
        {
            await using var db = _factory.CreateDbContext();

            return !await db.Sales.AsNoTracking().AnyAsync(sale => sale.StoreId == storeId)
                && !await db.SaleItems.AsNoTracking().AnyAsync(item => item.StoreId == storeId)
                && !await db.StockLevels.AsNoTracking().AnyAsync(stock => stock.StoreId == storeId)
                && !await db.StockMovements.AsNoTracking().AnyAsync(movement => movement.StoreId == storeId)
                && !await db.CashRegisterSessions.AsNoTracking().AnyAsync(session => session.StoreId == storeId)
                && !await db.CashRegisterCashMovements.AsNoTracking().AnyAsync(movement => movement.StoreId == storeId)
                && !await db.ProductStoreAssignments.AsNoTracking().AnyAsync(assignment => assignment.StoreId == storeId)
                && !await db.ProductVariants.AsNoTracking().AnyAsync(variant => variant.StoreId == storeId)
                && !await db.SellableVariants.AsNoTracking().AnyAsync(variant => variant.StoreId == storeId)
                && !await db.StockAdjustments.AsNoTracking().AnyAsync(adjustment => adjustment.StoreId == storeId)
                && !await db.StockTransfers.AsNoTracking().AnyAsync(transfer => transfer.FromStoreId == storeId || transfer.ToStoreId == storeId);
        }

        public async Task DeleteStoreAsync(int storeId)
        {
            await EnsureDemoCanWriteAsync();
            if (!await CanDeleteStoreAsync(storeId))
                throw new InvalidOperationException("This store cannot be deleted because it has historical records. Deactivate it instead.");

            var response = await _client.DeleteAsync($"api/Stores/{storeId}");
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(body)
                        ? "The store could not be deleted."
                        : body);
            }

            await DeleteLocalStoreAsync(storeId);
        }

        public async Task<List<UserStoreAssignment>> GetUserStoresAsync(int userId)
        {
            if (!CanUseAuthenticatedOnline())
                return new List<UserStoreAssignment>();

            try
            {
                return await _client.GetFromJsonAsync<List<UserStoreAssignment>>($"api/UserStores/{userId}") ?? new List<UserStoreAssignment>();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"User store lookup denied ({(int?)ex.StatusCode}). Returning no assignments.");
                return new List<UserStoreAssignment>();
            }
        }

        public async Task AssignUserToStoreAsync(int userId, int tenantId, int storeId, string role)
        {
            var response = await _client.PostAsJsonAsync("api/UserStores", new { userId, tenantId, storeId, role });
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to assign user to store ({(int)response.StatusCode}): {body}", null, response.StatusCode);
            }
        }

        public async Task RemoveUserFromStoreAsync(int userId, int storeId)
        {
            var response = await _client.DeleteAsync($"api/UserStores?userId={userId}&storeId={storeId}");
            response.EnsureSuccessStatusCode();
        }

        private bool CanUseAuthenticatedOnline() =>
            _net.IsOnline() && !string.IsNullOrWhiteSpace(_tokens.Token);

        private List<StoreRecord> GetFallbackStores()
        {
            if (_auth.IsTenantAdmin)
                return new List<StoreRecord>();

            var stores = new List<StoreRecord>();
            foreach (AssignedStore store in _auth.AllowedStores)
            {
                stores.Add(new StoreRecord
                {
                    Id = store.Id,
                    TenantId = store.TenantId,
                    Name = store.Name,
                    Code = string.Empty,
                    IsActive = true,
                });
            }

            return stores;
        }

        private async Task<List<StoreRecord>> GetLocalStoresAsync()
        {
            await using var db = _factory.CreateDbContext();
            var stores = await db.Stores.AsNoTracking().OrderBy(store => store.Name).ToListAsync();
            if (stores.Count > 0)
                return stores;

            var fallbackStores = GetFallbackStores();
            if (fallbackStores.Count > 0)
                await ReplaceLocalStoresAsync(fallbackStores);

            return fallbackStores;
        }

        private async Task ReplaceLocalStoresAsync(IEnumerable<StoreRecord> stores)
        {
            var storeList = stores?.ToList() ?? new List<StoreRecord>();

            await using var db = _factory.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Stores;");
            if (storeList.Count > 0)
                await db.Stores.AddRangeAsync(storeList);

            await db.SaveChangesAsync();
        }

        private async Task UpsertLocalStoreAsync(StoreRecord store)
        {
            await using var db = _factory.CreateDbContext();
            var existing = await db.Stores.FirstOrDefaultAsync(candidate => candidate.Id == store.Id);
            if (existing == null)
            {
                db.Stores.Add(store);
            }
            else
            {
                existing.TenantId = store.TenantId;
                existing.Name = store.Name;
                existing.Code = store.Code;
                existing.IsActive = store.IsActive;
            }

            await db.SaveChangesAsync();
        }

        private async Task DeleteLocalStoreAsync(int storeId)
        {
            await using var db = _factory.CreateDbContext();
            var existing = await db.Stores.FirstOrDefaultAsync(store => store.Id == storeId);
            if (existing == null)
                return;

            db.Stores.Remove(existing);
            await db.SaveChangesAsync();
        }

        private async Task EnsureDemoCanWriteAsync()
        {
            await _demoState.CheckExpirationAsync();
            if (_demoState.IsExpired)
                throw new InvalidOperationException("Demo session expired. Activate a license to use this in production.");
        }
    }
}
