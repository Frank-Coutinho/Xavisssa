using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface IDeviceIdentityService
{
    Task<DeviceIdentityDto> GetDeviceIdentityAsync();
    Task<string> GetDeviceFingerprintAsync();
}
