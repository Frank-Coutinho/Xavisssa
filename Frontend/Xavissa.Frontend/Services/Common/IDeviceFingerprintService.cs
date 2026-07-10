using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IDeviceFingerprintService
{
    Task<string> GetDeviceFingerprintAsync();
    Task<DeviceInfoDto> GetDeviceInfoAsync(int? tenantId = null);
}
