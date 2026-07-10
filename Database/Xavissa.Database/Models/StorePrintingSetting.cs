namespace Xavissa.Database.Models;

public class StorePrintingSetting : ITenantScopedEntity
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string? ReceiptHeaderOverride { get; set; }
    public string? ReceiptFooterOverride { get; set; }
    public string? PrinterName { get; set; }
    public string? BarcodeLabelTemplate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }
}
