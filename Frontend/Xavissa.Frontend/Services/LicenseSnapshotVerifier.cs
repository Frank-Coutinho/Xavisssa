using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicenseSnapshotVerifier : ILicenseSnapshotVerifier
{
    private const string TemporaryUnsignedSignature = "TEMPORARY-UNSIGNED-DEV-SNAPSHOT";
    private const string TemporaryUnsignedDemoSignature = "TEMPORARY-UNSIGNED-DEMO-SNAPSHOT";
    private readonly LicensingOptions _options;

    public LicenseSnapshotVerifier(IOptions<LicensingOptions> options)
    {
        _options = options.Value;
    }

    public LicenseStateResult Verify(
        LocalLicenseSnapshot? snapshot,
        DeviceIdentityDto device,
        int? expectedTenantId = null,
        DateTime? nowUtc = null)
    {
        var now = NormalizeUtc(nowUtc ?? DateTime.UtcNow);

        if (snapshot == null)
            return LicenseStateResult.Block(LicenseAccessStatus.NoLocalLicense, "This device has not been activated.");

        if (!IsSignatureValid(snapshot))
            return LicenseStateResult.Block(LicenseAccessStatus.InvalidSignature, "The local license snapshot signature is invalid.", snapshot);

        if (expectedTenantId.HasValue && snapshot.TenantId != expectedTenantId.Value)
            return LicenseStateResult.Block(LicenseAccessStatus.NeedsActivation, "This license belongs to a different tenant.", snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.DeviceFingerprint)
            || !string.Equals(snapshot.DeviceFingerprint, device.DeviceFingerprint, StringComparison.OrdinalIgnoreCase))
            return LicenseStateResult.Block(LicenseAccessStatus.DeviceNotActivated, "This device is not activated for the current license.", snapshot);

        if (!snapshot.ActivationId.HasValue || snapshot.ActivationId.Value <= 0)
            return LicenseStateResult.Block(LicenseAccessStatus.DeviceNotActivated, "The local license snapshot does not contain a server activation id.", snapshot);

        var status = snapshot.Status?.Trim() ?? string.Empty;
        if (status.Equals("Suspended", StringComparison.OrdinalIgnoreCase))
            return LicenseStateResult.Block(LicenseAccessStatus.Suspended, "This license is suspended. Contact support.", snapshot);

        if (status.Equals("DeviceDeactivated", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Deactivated", StringComparison.OrdinalIgnoreCase))
            return LicenseStateResult.Block(LicenseAccessStatus.DeviceDeactivated, "This device activation has been deactivated. Contact support.", snapshot);

        if (!IsAllowedStatus(snapshot))
            return LicenseStateResult.Block(LicenseAccessStatus.ValidationFailed, "This license is not active.", snapshot);

        if (snapshot.ExpiresAt.HasValue && NormalizeUtc(snapshot.ExpiresAt.Value) <= now)
        {
            if (snapshot.IsDemo)
                return LicenseStateResult.Block(LicenseAccessStatus.DemoExpired, "The demo session has expired.", snapshot);
            if (snapshot.IsTrial)
                return LicenseStateResult.Block(LicenseAccessStatus.TrialExpired, "The trial license has expired.", snapshot);
            return LicenseStateResult.Block(LicenseAccessStatus.Expired, "The license has expired.", snapshot);
        }

        if (NormalizeUtc(snapshot.SnapshotExpiresAt) <= now)
            return LicenseStateResult.Block(LicenseAccessStatus.OfflineLimitExceeded, "Connect to the internet to refresh the license.", snapshot);

        if (snapshot.GracePeriodEndsAt.HasValue && NormalizeUtc(snapshot.GracePeriodEndsAt.Value) <= now)
            return LicenseStateResult.Block(LicenseAccessStatus.OfflineLimitExceeded, "The offline grace period has ended. Connect to the internet to revalidate.", snapshot);

        if (!snapshot.IsDemo && snapshot.LastValidatedAt.HasValue)
        {
            var daysOffline = (now - NormalizeUtc(snapshot.LastValidatedAt.Value)).TotalDays;
            if (daysOffline > Math.Max(0, snapshot.MaxOfflineDays))
                return LicenseStateResult.Block(LicenseAccessStatus.OfflineLimitExceeded, "The allowed offline period has been exceeded.", snapshot);
        }

        return LicenseStateResult.Allow(LicenseAccessStatus.ValidOffline, "License is valid for offline use.", snapshot, offline: true);
    }

    private bool IsSignatureValid(LocalLicenseSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Signature))
            return false;

        if (_options.AllowTemporaryUnsignedSnapshots
            && string.Equals(snapshot.Signature, TemporaryUnsignedSignature, StringComparison.Ordinal))
            return true;

        if (_options.DemoEnabled
            && snapshot.IsDemo
            && string.Equals(snapshot.Signature, TemporaryUnsignedDemoSignature, StringComparison.Ordinal))
            return true;

        if (string.IsNullOrWhiteSpace(_options.LicensePublicKey))
            return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_options.LicensePublicKey);
            var payload = Encoding.UTF8.GetBytes(BuildSigningPayload(snapshot));
            var signature = Convert.FromBase64String(snapshot.Signature);
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildSigningPayload(LocalLicenseSnapshot snapshot)
    {
        var copy = new LocalLicenseSnapshot
        {
            TenantId = snapshot.TenantId,
            TenantCode = snapshot.TenantCode,
            TenantName = snapshot.TenantName,
            LicenseId = snapshot.LicenseId,
            LicensePublicCode = snapshot.LicensePublicCode,
            LicensePlanId = snapshot.LicensePlanId,
            PlanCode = snapshot.PlanCode,
            PlanName = snapshot.PlanName,
            ActivationId = snapshot.ActivationId,
            DeviceFingerprint = snapshot.DeviceFingerprint,
            Status = snapshot.Status,
            IsDemo = snapshot.IsDemo,
            IsTrial = snapshot.IsTrial,
            LicenseType = snapshot.LicenseType,
            PurchaseType = snapshot.PurchaseType,
            MaxStores = snapshot.MaxStores,
            MaxUsers = snapshot.MaxUsers,
            MaxDevices = snapshot.MaxDevices,
            MaxOfflineDays = snapshot.MaxOfflineDays,
            AllowsMultiStore = snapshot.AllowsMultiStore,
            AllowsAdvancedReports = snapshot.AllowsAdvancedReports,
            AllowsCloudSync = snapshot.AllowsCloudSync,
            AllowsBarcodePrinting = snapshot.AllowsBarcodePrinting,
            AllowsCustomReceipt = snapshot.AllowsCustomReceipt,
            AllowsDemoMode = snapshot.AllowsDemoMode,
            IssuedAt = snapshot.IssuedAt,
            ActivatedAt = snapshot.ActivatedAt,
            ExpiresAt = snapshot.ExpiresAt,
            LastValidatedAt = snapshot.LastValidatedAt,
            GracePeriodEndsAt = snapshot.GracePeriodEndsAt,
            SnapshotIssuedAt = snapshot.SnapshotIssuedAt,
            SnapshotExpiresAt = snapshot.SnapshotExpiresAt,
            Signature = string.Empty,
        };

        return JsonSerializer.Serialize(copy);
    }

    private static bool IsAllowedStatus(LocalLicenseSnapshot snapshot)
    {
        var status = snapshot.Status?.Trim() ?? string.Empty;
        return status.Equals("Active", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Trial", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            || (snapshot.IsTrial && status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            || (snapshot.IsDemo && status.Equals("Active", StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
