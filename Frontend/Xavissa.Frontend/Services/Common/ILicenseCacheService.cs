using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicenseCacheService
{
    string CachePath { get; }
    Task<LocalLicenseCacheDto?> LoadAsync();
    Task SaveSignedCacheAsync(string signedCacheJson);
    bool IsCacheSignatureValid(LocalLicenseCacheDto cache);
    bool HasUsableOfflineLicense(LocalLicenseCacheDto cache);
    bool IsLimitedMode(LocalLicenseCacheDto? cache);
}
