using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class LocalLicenseStore : ILocalLicenseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string SnapshotPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Xavissa"
            );
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "license-snapshot.json");
        }
    }

    public async Task<LocalLicenseSnapshot?> LoadAsync()
    {
        if (!File.Exists(SnapshotPath))
            return null;

        return JsonSerializer.Deserialize<LocalLicenseSnapshot>(
            await File.ReadAllTextAsync(SnapshotPath),
            JsonOptions
        );
    }

    public Task SaveAsync(LocalLicenseSnapshot snapshot)
    {
        return File.WriteAllTextAsync(
            SnapshotPath,
            JsonSerializer.Serialize(snapshot, JsonOptions)
        );
    }

    public Task ClearAsync()
    {
        if (File.Exists(SnapshotPath))
            File.Delete(SnapshotPath);

        return Task.CompletedTask;
    }
}
