using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILocalLicenseStore
{
    Task<LocalLicenseSnapshot?> LoadAsync();
    Task SaveAsync(LocalLicenseSnapshot snapshot);
    Task ClearAsync();
}
