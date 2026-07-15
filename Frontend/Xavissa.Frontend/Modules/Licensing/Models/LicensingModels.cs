using System;

namespace Xavissa.Frontend.Models;

public enum LicenseAccessStatus
{
    Unknown,
    NoLocalLicense,
    NeedsActivation,
    Activated,
    ValidOnline,
    ValidOffline,
    InGracePeriod,
    Expired,
    Suspended,
    DeviceNotActivated,
    DeviceDeactivated,
    DeviceLimitReached,
    TenantInactive,
    DemoExpired,
    TrialExpired,
    InvalidSignature,
    OfflineLimitExceeded,
    ServerUnavailable,
    ValidationFailed,
}

public enum LicenseFeature
{
    CloudSync,
    AdvancedReports,
    BarcodePrinting,
    CustomReceipt,
    DemoMode,
}

public sealed class LicensingOptions
{
    public Uri? LicensingApiBaseUrl { get; set; }
    public string? LicensePublicKey { get; set; }
    public bool OfflineValidationEnabled { get; set; } = true;
    public bool DemoEnabled { get; set; } = true;
    public int DemoDurationMinutes { get; set; } = 60;
    public Uri? DemoApiBaseUrl { get; set; }
    public string? DemoTemplateCode { get; set; }
    public string? DefaultDemoTemplate { get; set; }
    public bool DemoResetOnClose { get; set; } = true;
    public string? DemoContactWhatsApp { get; set; }
    public string? DemoVideoUrl { get; set; }
    public bool AllowTemporaryUnsignedSnapshots { get; set; }
}

public class DeviceIdentityDto
{
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string MachineUserName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string LocalDeviceId { get; set; } = string.Empty;
}

// Backwards-compatible name used by older call sites.
public class DeviceInfoDto : DeviceIdentityDto
{
    public int? TenantId { get; set; }
}

public class LocalDeviceIdentity
{
    public int Id { get; set; } = 1;
    public string LocalDeviceId { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LocalLicenseSnapshot
{
    public int Id { get; set; } = 1;
    public int? TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int? LicenseId { get; set; }
    public string LicensePublicCode { get; set; } = string.Empty;
    public int? LicensePlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int? ActivationId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDemo { get; set; }
    public bool IsTrial { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public string PurchaseType { get; set; } = string.Empty;
    public int? MaxStores { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxDevices { get; set; }
    public int MaxOfflineDays { get; set; } = 7;
    public bool AllowsMultiStore { get; set; }
    public bool AllowsAdvancedReports { get; set; }
    public bool AllowsCloudSync { get; set; }
    public bool AllowsBarcodePrinting { get; set; }
    public bool AllowsCustomReceipt { get; set; }
    public bool AllowsDemoMode { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }
    public DateTime SnapshotIssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime SnapshotExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public string Signature { get; set; } = string.Empty;

    public static LocalLicenseSnapshot CreatePosAccessSnapshot() =>
        new()
        {
            TenantName = "POS Workspace",
            PlanCode = "POS",
            PlanName = "POS",
            Status = "Active",
            AllowsMultiStore = true,
            AllowsAdvancedReports = true,
            AllowsCloudSync = true,
            AllowsBarcodePrinting = true,
            AllowsCustomReceipt = true,
            AllowsDemoMode = false,
            IssuedAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow,
            LastValidatedAt = DateTime.UtcNow,
            GracePeriodEndsAt = DateTime.UtcNow.AddYears(10),
            SnapshotExpiresAt = DateTime.UtcNow.AddYears(10),
        };
}

// Backwards-compatible name used by the previous cache service.
public class LocalLicenseCacheDto : LocalLicenseSnapshot
{
}

public class LicenseStateResult
{
    public LicenseAccessStatus Status { get; set; } = LicenseAccessStatus.Unknown;
    public bool CanOpenWorkspace { get; set; }
    public bool CanContinueOffline { get; set; }
    public bool ShouldShowActivation { get; set; }
    public bool ShouldShowBlocked { get; set; }
    public bool ShouldShowDemoExpired { get; set; }
    public bool ShouldShowTrialExpired { get; set; }
    public string Message { get; set; } = string.Empty;
    public LocalLicenseSnapshot? Snapshot { get; set; }

    public static LicenseStateResult Block(LicenseAccessStatus status, string message, LocalLicenseSnapshot? snapshot = null) =>
        new()
        {
            Status = status,
            Message = message,
            Snapshot = snapshot,
            ShouldShowActivation = status is LicenseAccessStatus.NoLocalLicense or LicenseAccessStatus.NeedsActivation,
            ShouldShowBlocked = status is not LicenseAccessStatus.NoLocalLicense and not LicenseAccessStatus.NeedsActivation,
            ShouldShowDemoExpired = status == LicenseAccessStatus.DemoExpired,
            ShouldShowTrialExpired = status == LicenseAccessStatus.TrialExpired,
        };

    public static LicenseStateResult Allow(LicenseAccessStatus status, string message, LocalLicenseSnapshot snapshot, bool offline) =>
        new()
        {
            Status = status,
            Message = message,
            Snapshot = snapshot,
            CanOpenWorkspace = true,
            CanContinueOffline = offline,
        };
}

public class ActivateLicenseRequest
{
    public string RawLicenseKey { get; set; } = string.Empty;
    public string? TenantCode { get; set; }
    public int? TenantId { get; set; }
    public string? Username { get; set; }
    public int? UserId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string MachineUserName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
}

public class ActivateLicenseResponse
{
    public bool Success { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public LocalLicenseSnapshot? LicenseSnapshot { get; set; }
    public bool RequiresLogin { get; set; }
    public TenantInfoDto? TenantInfo { get; set; }
    public int? ActivationId { get; set; }
}

public class ValidateActivationRequest
{
    public int? TenantId { get; set; }
    public int? LicenseId { get; set; }
    public int? ActivationId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public DateTime? LastKnownValidationAt { get; set; }
    public string ValidationType { get; set; } = "StartupValidation";
}

public class ValidateActivationResponse
{
    public bool Success { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public LocalLicenseSnapshot? LicenseSnapshot { get; set; }
    public bool ShouldBlockWorkspace { get; set; }
    public bool ShouldEnterGraceMode { get; set; }
    public bool ShouldForceReactivation { get; set; }
}

public class StartDemoSessionRequest
{
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string MachineUserName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string? OptionalLeadName { get; set; }
    public string? OptionalLeadPhone { get; set; }
    public string? OptionalLeadEmail { get; set; }
    public string? DemoTemplateCode { get; set; }
    public bool UseDefaultTemplate { get; set; } = true;
    public bool ResetOnClose { get; set; } = true;
}

public class StartDemoSessionResponse
{
    public bool Success { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int? LicenseId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool ResetOnClose { get; set; } = true;
    public LocalLicenseSnapshot? DemoLicenseSnapshot { get; set; }
    public bool DemoModeEnabled { get; set; }
    public string DemoToken { get; set; } = string.Empty;
    public DemoCredentialsDto? DemoCredentials { get; set; }

    public LocalLicenseSnapshot? LicenseSnapshot
    {
        get => DemoLicenseSnapshot;
        set => DemoLicenseSnapshot = value;
    }
}

// Backwards-compatible name used by older demo call sites.
public class DemoStartResponse : StartDemoSessionResponse
{
}

public class ValidateDemoSessionRequest
{
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public int? LicenseId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
}

public class ValidateDemoSessionResponse
{
    public bool Success { get; set; }
    public bool IsExpired { get; set; }
    public string? FailureMessage { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RemainingSeconds { get; set; }
    public LocalLicenseSnapshot? DemoLicenseSnapshot { get; set; }
}

public class EndDemoSessionRequest
{
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public int? LicenseId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
}

public class TrackDemoEventRequest
{
    public int? DemoSessionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Description { get; set; }
}

public enum DemoModeStatus
{
    NotStarted,
    Starting,
    Active,
    Expired,
    Failed,
    OfflineUnavailable,
}

public class DemoSessionState
{
    public int? DemoSessionId { get; set; }
    public int? TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int? LicenseId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public bool ResetOnClose { get; set; } = true;
    public bool IsActive { get; set; }
    public int RemainingSeconds { get; set; }
    public DemoModeStatus Status { get; set; } = DemoModeStatus.NotStarted;
}

public class DeactivateCurrentDeviceRequest
{
    public int? TenantId { get; set; }
    public int? LicenseId { get; set; }
    public int? ActivationId { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
}

public class TenantInfoDto
{
    public int? TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}

public class DemoCredentialsDto
{
    public string Username { get; set; } = "demo";
    public string Password { get; set; } = string.Empty;
    public string? WorkspaceToken { get; set; }
}

public class EffectiveLicenseLimitsDto
{
    public int TenantId { get; set; }
    public int LicenseId { get; set; }
    public string LicensePublicCode { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? MaxStores { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxDevices { get; set; }
    public int MaxOfflineDays { get; set; }
    public bool AllowsMultiStore { get; set; }
    public bool AllowsAdvancedReports { get; set; }
    public bool AllowsCloudSync { get; set; }
    public bool AllowsBarcodePrinting { get; set; }
    public bool AllowsCustomReceipt { get; set; }
    public bool AllowsDemoMode { get; set; }
    public int StoresUsed { get; set; }
    public int UsersUsed { get; set; }
    public int DevicesUsed { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }
}

public class LicenseOperationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public EffectiveLicenseLimitsDto? License { get; set; }
    public string? SignedCache { get; set; }
}
