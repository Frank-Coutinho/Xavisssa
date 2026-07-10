using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class DeviceIdentityService : IDeviceIdentityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<DeviceIdentityDto> GetDeviceIdentityAsync()
    {
        var persisted = await GetOrCreatePersistedIdentityAsync();

        return new DeviceIdentityDto
        {
            DeviceFingerprint = persisted.DeviceFingerprint,
            DeviceName = Environment.MachineName,
            MachineUserName = Environment.UserName,
            OSVersion = Environment.OSVersion.VersionString,
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
            LocalDeviceId = persisted.LocalDeviceId,
        };
    }

    public async Task<string> GetDeviceFingerprintAsync()
    {
        var identity = await GetDeviceIdentityAsync();
        return identity.DeviceFingerprint;
    }

    private static async Task<LocalDeviceIdentity> GetOrCreatePersistedIdentityAsync()
    {
        var path = GetIdentityPath();
        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<LocalDeviceIdentity>(await File.ReadAllTextAsync(path));
            if (existing != null
                && !string.IsNullOrWhiteSpace(existing.LocalDeviceId)
                && !string.IsNullOrWhiteSpace(existing.DeviceFingerprint))
                return existing;
        }

        var localDeviceId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var material = string.Join("|", new[]
        {
            "Xavissa",
            localDeviceId,
            Environment.MachineName,
            Environment.OSVersion.Platform.ToString(),
            TryGetWindowsMachineGuid(),
        });

        var identity = new LocalDeviceIdentity
        {
            LocalDeviceId = localDeviceId,
            DeviceFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(identity, JsonOptions));
        return identity;
    }

    private static string GetIdentityPath()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xavissa");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "device-identity.json");
    }

    private static string? TryGetWindowsMachineGuid()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return null;

            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
