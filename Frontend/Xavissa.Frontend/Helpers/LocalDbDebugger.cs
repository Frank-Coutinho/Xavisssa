using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;

public static class LocalDbDebugger
{
    private static LocalDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={LocalDbContext.GetLocalDbPath()}")
            .Options;

        return new LocalDbContext(options);
    }

    public static void PrintOfflineSales()
    {
        using var db = CreateDb();

        Console.WriteLine($"Local DB path: {db.Database.GetDbConnection().DataSource}");

        var sales = db.Sales.Include(s => s.Items).AsNoTracking().ToList();

        if (sales.Count == 0)
        {
            Console.WriteLine("No sales found in offline DB.");
            return;
        }

        foreach (var sale in sales)
        {
            Console.WriteLine(
                $"Sale Id={sale.Id}, Synced={sale.Synced}, Timestamp={sale.Timestamp}, Total={sale.TotalAmount}"
            );

            foreach (var item in sale.Items)
                Console.WriteLine(
                    $"  Item Id={item.Id}, ProductId={item.ProductId}, Qty={item.Quantity}, Price={item.UnitPrice}"
                );
        }
    }

    public static void PrintAllProducts()
    {
        using var db = CreateDb();

        var products = db.Products.AsNoTracking().ToList();
        Console.WriteLine($"Products count: {products.Count}");

        foreach (var p in products)
            Console.WriteLine($"Product Id={p.Id}, Name={p.Name}, Stock={p.StockQuantity}");
    }
}
