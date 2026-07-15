using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IUserRepositoryOnline
    {
        Task<List<User>> FetchAllFromServerAsync();
        Task CreateAsync(CreateUserRequest req);
        Task UpdateStatusAsync(int id, bool isActive);
        Task DeleteAsync(int id);
    }
}
