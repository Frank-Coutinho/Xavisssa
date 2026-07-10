using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicenseFeatureGate
{
    Task<(bool Allowed, string? Message)> EnsureFeatureAsync(LicenseFeature feature, int? tenantId = null);
    Task<bool> IsFeatureAllowedAsync(LicenseFeature feature, int? tenantId = null);
}
