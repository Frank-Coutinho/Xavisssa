using System;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public interface ILicenseSnapshotVerifier
{
    LicenseStateResult Verify(
        LocalLicenseSnapshot? snapshot,
        DeviceIdentityDto device,
        int? expectedTenantId = null,
        DateTime? nowUtc = null);
}
