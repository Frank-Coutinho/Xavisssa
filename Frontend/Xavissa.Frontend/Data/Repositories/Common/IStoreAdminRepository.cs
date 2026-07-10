using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IStoreAdminRepository
    {
        Task<List<StoreRecord>> GetStoresAsync();
        Task<StoreRecord> CreateStoreAsync(StoreRecord store);
        Task<StoreRecord> UpdateStoreAsync(StoreRecord store);
        Task<StoreRecord> DeactivateStoreAsync(StoreRecord store);
        Task<bool> CanDeleteStoreAsync(int storeId);
        Task DeleteStoreAsync(int storeId);
        Task<List<UserStoreAssignment>> GetUserStoresAsync(int userId);
        Task AssignUserToStoreAsync(int userId, int tenantId, int storeId, string role);
        Task RemoveUserFromStoreAsync(int userId, int storeId);
    }
}
