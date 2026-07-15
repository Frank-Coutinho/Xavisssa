using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicensingApiClient
{
    Task<ActivateLicenseResponse> ActivateLicenseAsync(ActivateLicenseRequest request);
    Task<ValidateActivationResponse> ValidateActivationAsync(ValidateActivationRequest request);
    Task<StartDemoSessionResponse> StartDemoSessionAsync(StartDemoSessionRequest request);
    Task<ValidateActivationResponse> RefreshLicenseSnapshotAsync(ValidateActivationRequest request);
    Task<bool> DeactivateCurrentDeviceAsync(DeactivateCurrentDeviceRequest request);
    Task<EffectiveLicenseLimitsDto?> GetTenantLicenseStatusAsync(int tenantId);
}
