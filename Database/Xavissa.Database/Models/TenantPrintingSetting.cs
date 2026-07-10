namespace Xavissa.Database.Models;

public class TenantPrintingSetting
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public string? PaperWidth { get; set; }
    public bool ShowLogo { get; set; }
    public string? PrinterName { get; set; }
    public string? BarcodeLabelTemplate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }
}
