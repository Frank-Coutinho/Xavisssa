using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicenseStateService : ILicenseStateService
{
    private readonly ILocalLicenseStore _store;
    private readonly ILicenseSnapshotVerifier _verifier;
    private readonly IDeviceIdentityService _deviceIdentity;
    private readonly ILicensingApiClient _api;
    private readonly IDemoApiClient _demoApi;
    private readonly IDemoStateService _demoState;
    private readonly IDemoWorkspaceSeeder _demoSeeder;
    private readonly IWorkspaceService _workspace;
    private readonly IConnectivityService _connectivity;
    private readonly LicensingOptions _options;

    public LicenseStateService(
        ILocalLicenseStore store,
        ILicenseSnapshotVerifier verifier,
        IDeviceIdentityService deviceIdentity,
        ILicensingApiClient api,
        IDemoApiClient demoApi,
        IDemoStateService demoState,
        IDemoWorkspaceSeeder demoSeeder,
        IWorkspaceService workspace,
        IConnectivityService connectivity,
        IOptions<LicensingOptions> options)
    {
        _store = store;
        _verifier = verifier;
        _deviceIdentity = deviceIdentity;
        _api = api;
        _demoApi = demoApi;
        _demoState = demoState;
        _demoSeeder = demoSeeder;
        _workspace = workspace;
        _connectivity = connectivity;
        _options = options.Value;
    }

    public LicenseStateResult Current { get; private set; } =
        LicenseStateResult.Allow(
            LicenseAccessStatus.ValidOffline,
            "POS workspace available.",
            LocalLicenseSnapshot.CreatePosAccessSnapshot(),
            offline: true);

    public event Action<LicenseStateResult>? StateChanged;

    public async Task<LicenseStateResult> EvaluateAsync(int? tenantId = null, bool preferOnlineValidation = true, string validationType = "StartupValidation")
    {
        await Task.CompletedTask;
        return Publish(CreatePosAccessResult(tenantId));
    }

    public async Task<LicenseStateResult> ActivateAsync(string rawLicenseKey, int? tenantId = null, string? tenantCode = null, string? username = null, int? userId = null)
    {
        await Task.CompletedTask;
        return Publish(CreatePosAccessResult(tenantId));
    }

    public async Task<LicenseStateResult> StartDemoAsync()
    {
        await Task.CompletedTask;
        return Publish(LicenseStateResult.Block(LicenseAccessStatus.ValidationFailed, "Demo mode is currently disabled."));
    }

    public async Task<LicenseStateResult> RefreshOnlineAsync(string validationType)
    {
        await Task.CompletedTask;
        return Publish(CreatePosAccessResult());
    }

    private static LicenseStateResult CreatePosAccessResult(int? tenantId = null)
    {
        var snapshot = LocalLicenseSnapshot.CreatePosAccessSnapshot();
        snapshot.TenantId = tenantId;
        return LicenseStateResult.Allow(LicenseAccessStatus.ValidOffline, "POS workspace available.", snapshot, offline: true);
    }

    private async Task<LicenseStateResult> ValidateOnlineAsync(LocalLicenseSnapshot snapshot, DeviceIdentityDto device, string validationType, bool fallbackToLocal)
    {
        var response = await _api.ValidateActivationAsync(new ValidateActivationRequest
        {
            TenantId = snapshot.TenantId,
            LicenseId = snapshot.LicenseId,
            ActivationId = snapshot.ActivationId,
            DeviceFingerprint = device.DeviceFingerprint,
            AppVersion = device.AppVersion,
            OSVersion = device.OSVersion,
            LastKnownValidationAt = snapshot.LastValidatedAt,
            ValidationType = validationType,
        });

        if (!response.Success)
        {
            if (response.FailureCode == "SERVER_UNAVAILABLE" && fallbackToLocal)
            {
                var local = _verifier.Verify(snapshot, device, snapshot.TenantId);
                return Publish(local.CanOpenWorkspace
                    ? LicenseStateResult.Allow(LicenseAccessStatus.ValidOffline, "Licensing server is unavailable; continuing within the offline window.", snapshot, offline: true)
                    : local);
            }

            return Publish(MapFailure(response.FailureCode, response.FailureMessage ?? "License validation failed.", snapshot));
        }

        var freshSnapshot = response.LicenseSnapshot ?? snapshot;
        var verified = _verifier.Verify(freshSnapshot, device, freshSnapshot.TenantId);
        if (!verified.CanOpenWorkspace)
            return Publish(verified);

        await _store.SaveAsync(freshSnapshot);
        return Publish(LicenseStateResult.Allow(
            response.ShouldEnterGraceMode ? LicenseAccessStatus.InGracePeriod : LicenseAccessStatus.ValidOnline,
            "License validated online.",
            freshSnapshot,
            offline: false));
    }

    private LicenseStateResult Publish(LicenseStateResult result)
    {
        Current = result;
        StateChanged?.Invoke(result);
        return result;
    }

    private static LicenseStateResult MapFailure(string? code, string message, LocalLicenseSnapshot? snapshot = null)
    {
        var status = (code ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "INVALID_KEY" => LicenseAccessStatus.NeedsActivation,
            "LICENSE_SUSPENDED" => LicenseAccessStatus.Suspended,
            "SUSPENDED" => LicenseAccessStatus.Suspended,
            "EXPIRED_LICENSE" => LicenseAccessStatus.Expired,
            "LICENSE_EXPIRED" => LicenseAccessStatus.Expired,
            "TRIAL_EXPIRED" => LicenseAccessStatus.TrialExpired,
            "DEMO_EXPIRED" => LicenseAccessStatus.DemoExpired,
            "TENANT_INACTIVE" => LicenseAccessStatus.TenantInactive,
            "DEVICE_LIMIT_REACHED" => LicenseAccessStatus.DeviceLimitReached,
            "DEVICE_DEACTIVATED" => LicenseAccessStatus.DeviceDeactivated,
            "DEVICE_NOT_ACTIVATED" => LicenseAccessStatus.DeviceNotActivated,
            "SERVER_UNAVAILABLE" => LicenseAccessStatus.ServerUnavailable,
            _ => LicenseAccessStatus.ValidationFailed,
        };

        if (status == LicenseAccessStatus.DeviceLimitReached)
            message = "Device access could not be confirmed.";

        return LicenseStateResult.Block(status, message, snapshot);
    }
}
