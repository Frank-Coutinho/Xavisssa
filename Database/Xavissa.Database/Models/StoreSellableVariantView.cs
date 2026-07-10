using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Database.Models;

[Table("vw_store_sellable_variants")]
public class StoreSellableVariantView
{
    [Column("VariantId")]
    public int VariantId { get; set; }

    [Column("StoreProductId")]
    public int StoreProductId { get; set; }

    [Column("ProductId")]
    public int ProductId { get; set; }

    [Column("TenantId")]
    public int TenantId { get; set; }

    [Column("StoreId")]
    public int StoreId { get; set; }

    [Column("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    [Column("VariantLabel")]
    public string? VariantLabel { get; set; }

    [Column("Barcode")]
    public string? Barcode { get; set; }

    [Column("SKU")]
    public string? SKU { get; set; }

    [Column("Price")]
    public decimal Price { get; set; }

    [Column("QuantityOnHand")]
    public int QuantityOnHand { get; set; }

    [Column("IsSellable")]
    public bool IsSellable { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}
