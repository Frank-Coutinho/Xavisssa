using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IUserRepositoryOffline
    {
        Task<List<OfflineIdentity>> GetAllAsync();
        Task<OfflineIdentity?> GetByUsernameAsync(string username);
        Task SyncFromServerAsync(List<User> usersFromServer);
    }
}
