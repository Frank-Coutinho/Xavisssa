using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class SyncConflictService
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;

    public SyncConflictService(XavissaDbContext db, TenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public Task<List<SyncConflict>> ListAsync() =>
        _db.SyncConflicts.OrderByDescending(x => x.CreatedAt).ToListAsync();

    public Task<SyncConflict?> GetAsync(int id) =>
        _db.SyncConflicts.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<SyncConflict> CreateAsync(
        int tenantId,
        int? storeId,
        string entityName,
        Guid? entitySyncId,
        string conflictType,
        string? localPayloadJson,
        string? serverPayloadJson = null)
    {
        var conflict = new SyncConflict
        {
            TenantId = tenantId,
            StoreId = storeId,
            EntityName = entityName,
            EntitySyncId = entitySyncId,
            ConflictType = conflictType,
            LocalPayloadJson = localPayloadJson,
            ServerPayloadJson = serverPayloadJson,
            ResolutionStatus = SyncConflictResolutionStatuses.NeedsReview,
        };
        _db.SyncConflicts.Add(conflict);
        await _db.SaveChangesAsync();
        return conflict;
    }

    public Task<SyncConflict> ResolveAsync(int id, string? notes) =>
        SetStatusAsync(id, SyncConflictResolutionStatuses.Resolved, notes);

    public Task<SyncConflict> IgnoreAsync(int id, string? notes) =>
        SetStatusAsync(id, SyncConflictResolutionStatuses.Ignored, notes);

    private async Task<SyncConflict> SetStatusAsync(int id, string status, string? notes)
    {
        var conflict = await _db.SyncConflicts.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Sync conflict was not found.");
        if (conflict.StoreId.HasValue && !_tenantAccess.CanManageStore(conflict.StoreId.Value))
            throw new UnauthorizedAccessException("Unauthorized sync conflict.");
        if (conflict.TenantId.HasValue && !_tenantAccess.CanManageTenant(conflict.TenantId.Value) && !_tenantAccess.IsPlatformAdmin && !_tenantAccess.IsSupport)
            throw new UnauthorizedAccessException("Unauthorized sync conflict.");

        conflict.ResolutionStatus = status;
        conflict.ResolutionNotes = notes;
        conflict.ResolvedAt = DateTime.UtcNow;
        conflict.ResolvedByUserId = _tenantAccess.CurrentUserId;
        await _db.SaveChangesAsync();
        return conflict;
    }
}
