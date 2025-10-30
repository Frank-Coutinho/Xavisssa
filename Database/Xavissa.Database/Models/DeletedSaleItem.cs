namespace Xavissa.Database.Models;

public class DeletedSaleItem
{
    public int Id { get; set; }
    public int DeletedSaleId { get; set; }
    public int ProductId { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}