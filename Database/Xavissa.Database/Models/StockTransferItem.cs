namespace Xavissa.Database.Models;

public class StockTransferItem : IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    public int QuantityRequested { get; set; }
    public int? QuantityApproved { get; set; }
    public int? QuantitySent { get; set; }
    public int? QuantityReceived { get; set; }
    public string? Notes { get; set; }
}
