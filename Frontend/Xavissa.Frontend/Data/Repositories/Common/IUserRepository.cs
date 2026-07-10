using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    // IUserRepository.cs — read-only for both online & offline
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByUsernameAsync(string username);

        Task CreateAsync(CreateUserRequest req);
        Task UpdateStatusAsync(int userId, bool isActive);
        Task DeleteAsync(int userId);

        Task SyncFromServerAsync();
    }
}
