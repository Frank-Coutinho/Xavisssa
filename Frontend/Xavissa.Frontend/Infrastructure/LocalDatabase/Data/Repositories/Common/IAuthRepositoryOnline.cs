using System.Threading.Tasks;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IAuthRepositoryOnline
    {
        Task<LoginResponse?> LoginAsync(string username, string password);
        Task<LoginResponse?> SelectStoreAsync(string username, string password, int storeId);
    }
}
