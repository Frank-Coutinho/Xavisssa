using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicenseClient : ILicenseClient
{
    private readonly ILicenseStateService _licenseState;
    private readonly ILicensingApiClient _api;

    public LicenseClient(ILicenseStateService licenseState, ILicensingApiClient api)
    {
        _licenseState = licenseState;
        _api = api;
    }

    public async Task<LicenseOperationResult?> ActivateAsync(string licenseKey, DeviceInfoDto deviceInfo)
    {
        var result = await _licenseState.ActivateAsync(
            licenseKey,
            deviceInfo.TenantId,
            username: null,
            userId: null);

        return new LicenseOperationResult
        {
            Success = result.CanOpenWorkspace,
            Error = result.CanOpenWorkspace ? null : result.Message,
            SignedCache = result.Snapshot == null ? null : System.Text.Json.JsonSerializer.Serialize(result.Snapshot),
        };
    }

    public Task<EffectiveLicenseLimitsDto?> GetUsageAsync(int tenantId) =>
        _api.GetTenantLicenseStatusAsync(tenantId);

    public async Task<DemoStartResponse?> StartDemoAsync(DeviceInfoDto deviceInfo)
    {
        var result = await _licenseState.StartDemoAsync();
        if (!result.CanOpenWorkspace || result.Snapshot == null)
            return null;

        return new DemoStartResponse
        {
            Success = true,
            TenantId = result.Snapshot.TenantId,
            LicenseId = result.Snapshot.LicenseId,
            DemoSessionId = result.Snapshot.ActivationId,
            LicenseSnapshot = result.Snapshot,
            ExpiresAt = result.Snapshot.ExpiresAt ?? result.Snapshot.SnapshotExpiresAt,
        };
    }
}
