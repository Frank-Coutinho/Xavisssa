using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public class DemoCleanupService : IDemoCleanupService
{
    private readonly IDemoStateService _demoState;
    private readonly IWorkspaceService _workspace;
    private readonly ILocalLicenseStore _licenseStore;
    private readonly LicensingOptions _options;

    public DemoCleanupService(
        IDemoStateService demoState,
        IWorkspaceService workspace,
        ILocalLicenseStore licenseStore,
        IOptions<LicensingOptions> options)
    {
        _demoState = demoState;
        _workspace = workspace;
        _licenseStore = licenseStore;
        _options = options.Value;
    }

    public async Task CleanupExpiredDemoAsync()
    {
        await _demoState.LoadAsync();
        if (!_demoState.IsExpired)
            return;

        await ClearDemoOnlyAsync();
    }

    public async Task CleanupOnCloseAsync()
    {
        await _demoState.LoadAsync();
        if (!_options.DemoResetOnClose || !_demoState.Current.ResetOnClose)
            return;

        await ClearDemoOnlyAsync();
    }

    private async Task ClearDemoOnlyAsync()
    {
        var snapshot = await _licenseStore.LoadAsync();
        if (snapshot?.IsDemo == true)
            await _licenseStore.ClearAsync();

        await _demoState.ClearAsync();

        TryDeleteIfExists(_workspace.DemoDbPath);
        TryDeleteIfExists(_workspace.DemoDbPath + "-wal");
        TryDeleteIfExists(_workspace.DemoDbPath + "-shm");
    }

    private static void TryDeleteIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var fullPath = Path.GetFullPath(path);
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xavissa", "Workspaces", "Demo");
        var fullRoot = Path.GetFullPath(root);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to delete a non-demo workspace file.");

        try
        {
            File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
