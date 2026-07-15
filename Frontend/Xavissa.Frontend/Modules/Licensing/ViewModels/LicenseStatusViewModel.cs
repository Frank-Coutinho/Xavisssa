using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels;

public class LicenseStatusViewModel : ViewModelBase
{
    private readonly ILicenseStateService _licenseState;
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;
    private readonly IAuthService _auth;

    private string _summary = "License status unavailable.";
    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    public LicenseStatusViewModel(ILicenseStateService licenseState, IDbContextFactory<LocalDbContext> dbFactory, IAuthService auth)
    {
        _licenseState = licenseState;
        _dbFactory = dbFactory;
        _auth = auth;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var result = await _licenseState.EvaluateAsync(_auth.SelectedTenantId, preferOnlineValidation: false, validationType: "OfflineGraceCheck");
        if (result.Snapshot == null)
        {
            Summary = result.Message;
            return;
        }

        await using var db = _dbFactory.CreateDbContext();
        var tenantId = result.Snapshot.TenantId ?? 0;
        var stores = await db.Stores.AsNoTracking().CountAsync(x => x.TenantId == tenantId && x.IsActive);
        var users = await db.OfflineIdentities.AsNoTracking().CountAsync(x => x.IsActive);

        Summary =
            $"License {result.Snapshot.LicensePublicCode}\n" +
            $"Plan: {result.Snapshot.PlanName} ({result.Snapshot.PlanCode})\n" +
            $"Status: {result.Snapshot.Status} | Type: {result.Snapshot.LicenseType} | Purchase: {result.Snapshot.PurchaseType}\n" +
            $"Trial: {result.Snapshot.IsTrial} | Demo: {result.Snapshot.IsDemo}\n" +
            $"Activated: {result.Snapshot.ActivatedAt?.ToLocalTime():g} | Expires: {result.Snapshot.ExpiresAt?.ToLocalTime():g}\n" +
            $"Last validated: {result.Snapshot.LastValidatedAt?.ToLocalTime():g}\n" +
            $"Stores: {stores}/{FormatLimit(result.Snapshot.MaxStores)} | Users: {users}/{FormatLimit(result.Snapshot.MaxUsers)} | Devices: {FormatLimit(result.Snapshot.MaxDevices)}\n" +
            $"Offline days: {result.Snapshot.MaxOfflineDays}\n" +
            $"Cloud sync: {result.Snapshot.AllowsCloudSync} | Advanced reports: {result.Snapshot.AllowsAdvancedReports} | Barcode printing: {result.Snapshot.AllowsBarcodePrinting} | Custom receipt: {result.Snapshot.AllowsCustomReceipt}\n" +
            $"Current device activation: {(result.CanOpenWorkspace ? "Active" : result.Status)}\n" +
            "For upgrades or device replacement, contact Xavissa support.";
    }

    private static string FormatLimit(int? value) => value.HasValue ? value.Value.ToString() : "Unlimited";
}
