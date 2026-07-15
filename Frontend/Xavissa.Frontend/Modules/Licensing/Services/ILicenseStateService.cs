using System;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicenseStateService
{
    LicenseStateResult Current { get; }
    event Action<LicenseStateResult>? StateChanged;
    Task<LicenseStateResult> EvaluateAsync(int? tenantId = null, bool preferOnlineValidation = true, string validationType = "StartupValidation");
    Task<LicenseStateResult> ActivateAsync(string rawLicenseKey, int? tenantId = null, string? tenantCode = null, string? username = null, int? userId = null);
    Task<LicenseStateResult> StartDemoAsync();
    Task<LicenseStateResult> RefreshOnlineAsync(string validationType);
}
