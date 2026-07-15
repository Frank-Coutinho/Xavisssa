using System;
using System.ComponentModel.DataAnnotations;

namespace Xavissa.Frontend.Models
{
    public class SellableVariantSnapshot
    {
        [Key]
        public int VariantId { get; set; }
        public int StoreProductId { get; set; }
        public int ProductId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string VariantLabel { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int QuantityOnHand { get; set; }
        public bool IsSellable { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
