using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicenseClient
{
    Task<LicenseOperationResult?> ActivateAsync(string licenseKey, DeviceInfoDto deviceInfo);
    Task<EffectiveLicenseLimitsDto?> GetUsageAsync(int tenantId);
    Task<DemoStartResponse?> StartDemoAsync(DeviceInfoDto deviceInfo);
}
