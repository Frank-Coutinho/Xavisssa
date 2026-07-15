using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LicenseCacheService : ILicenseCacheService
{
    private readonly ILocalLicenseStore _store;
    private readonly ILicenseSnapshotVerifier _verifier;
    private readonly IDeviceIdentityService _deviceIdentity;

    public LicenseCacheService(
        ILocalLicenseStore store,
        ILicenseSnapshotVerifier verifier,
        IDeviceIdentityService deviceIdentity)
    {
        _store = store;
        _verifier = verifier;
        _deviceIdentity = deviceIdentity;
    }

    public string CachePath => (_store as LocalLicenseStore)?.SnapshotPath ?? string.Empty;

    public async Task<LocalLicenseCacheDto?> LoadAsync()
    {
        var snapshot = await _store.LoadAsync();
        if (snapshot == null)
            return null;

        var result = _verifier.Verify(snapshot, await _deviceIdentity.GetDeviceIdentityAsync());
        if (!result.CanOpenWorkspace)
            return null;

        return Clone<LocalLicenseCacheDto>(snapshot);
    }

    public async Task SaveSignedCacheAsync(string signedCacheJson)
    {
        var snapshot = JsonSerializer.Deserialize<LocalLicenseSnapshot>(signedCacheJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("The license snapshot response was empty.");

        var result = _verifier.Verify(snapshot, await _deviceIdentity.GetDeviceIdentityAsync(), snapshot.TenantId);
        if (!result.CanOpenWorkspace)
            throw new InvalidOperationException(result.Message);

        await _store.SaveAsync(snapshot);
    }

    public bool IsCacheSignatureValid(LocalLicenseCacheDto cache)
    {
        return _verifier.Verify(cache, _deviceIdentity.GetDeviceIdentityAsync().GetAwaiter().GetResult()).CanOpenWorkspace;
    }

    public bool HasUsableOfflineLicense(LocalLicenseCacheDto cache) => IsCacheSignatureValid(cache);

    public bool IsLimitedMode(LocalLicenseCacheDto? cache) => cache == null || !IsCacheSignatureValid(cache);

    private static T Clone<T>(object source)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
    }
}
