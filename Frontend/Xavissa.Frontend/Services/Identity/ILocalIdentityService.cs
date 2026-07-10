using System.Threading.Tasks;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models.Auth;


public interface ILocalIdentityService
{
    Task SaveFromOnlineLoginAsync(LoginResponse login, string password);

    Task<OfflineIdentity?> ValidateOfflineLoginAsync(string username, string password);

    Task ClearCachedTokenAsync(string username);
}
