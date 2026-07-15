using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicenseFeatureGate : ILicenseFeatureGate
{
    private readonly ILicenseStateService _licenseState;

    public LicenseFeatureGate(ILicenseStateService licenseState)
    {
        _licenseState = licenseState;
    }

    public async Task<(bool Allowed, string? Message)> EnsureFeatureAsync(LicenseFeature feature, int? tenantId = null)
    {
        await Task.CompletedTask;
        return (true, null);
    }

    public async Task<bool> IsFeatureAllowedAsync(LicenseFeature feature, int? tenantId = null)
    {
        var check = await EnsureFeatureAsync(feature, tenantId);
        return check.Allowed;
    }

}
