using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class DeviceFingerprintService : IDeviceFingerprintService
{
    private readonly IDeviceIdentityService _deviceIdentity;

    public DeviceFingerprintService(IDeviceIdentityService deviceIdentity)
    {
        _deviceIdentity = deviceIdentity;
    }

    public Task<string> GetDeviceFingerprintAsync() => _deviceIdentity.GetDeviceFingerprintAsync();

    public async Task<DeviceInfoDto> GetDeviceInfoAsync(int? tenantId = null)
    {
        var identity = await _deviceIdentity.GetDeviceIdentityAsync();
        return new DeviceInfoDto
        {
            TenantId = tenantId,
            DeviceFingerprint = identity.DeviceFingerprint,
            DeviceName = identity.DeviceName,
            MachineUserName = identity.MachineUserName,
            AppVersion = identity.AppVersion,
            OSVersion = identity.OSVersion,
            LocalDeviceId = identity.LocalDeviceId,
        };
    }
}
