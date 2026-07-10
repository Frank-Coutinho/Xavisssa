namespace Xavissa.Database.Models;

public static class SyncConflictTypes
{
    public const string InsufficientRemoteStock = "InsufficientRemoteStock";
    public const string InactiveVariantSoldOffline = "InactiveVariantSoldOffline";
    public const string DeletedProductUsedOffline = "DeletedProductUsedOffline";
    public const string DuplicateReceiptNumber = "DuplicateReceiptNumber";
    public const string OfflinePolicyConflict = "OfflinePolicyConflict";
    public const string UnknownUploadConflict = "UnknownUploadConflict";
}

public static class SyncConflictResolutionStatuses
{
    public const string Open = "Open";
    public const string NeedsReview = "NeedsReview";
    public const string Resolved = "Resolved";
    public const string Ignored = "Ignored";
}

public class SyncConflict : ITenantScopedEntity
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int? StoreId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntitySyncId { get; set; }
    public string ConflictType { get; set; } = SyncConflictTypes.UnknownUploadConflict;
    public string? LocalPayloadJson { get; set; }
    public string? ServerPayloadJson { get; set; }
    public string ResolutionStatus { get; set; } = SyncConflictResolutionStatuses.Open;
    public string? ResolutionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
}
