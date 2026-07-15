using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;

public static class LocalDbIntegrityChecker
{
    private static LocalDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite($"Data Source={LocalDbContext.GetLocalDbPath()}")
            .Options;

        return new LocalDbContext(options);
    }

    public static void CheckSalesIntegrity()
    {
        using var db = CreateDb();

        Console.WriteLine("🔍 Running FULL SALES INTEGRITY CHECK…");

        var sales = db.Sales.AsNoTracking().ToList();
        var items = db.SaleItems.AsNoTracking().ToList();
        var products = db.Products.AsNoTracking().ToList();

        Console.WriteLine(
            $"Sales: {sales.Count}, Items: {items.Count}, Products: {products.Count}"
        );

        // 1️⃣ Orphan SaleItems: SaleId missing in Sales table
        Console.WriteLine("\n--- Checking orphan SaleItems (SaleId missing) ---");
        var orphanSaleId = items.Where(i => !sales.Any(s => s.Id == i.SaleId)).ToList();

        if (orphanSaleId.Any())
        {
            Console.WriteLine("❌ Found orphan SaleItems (SaleId not in Sales):");
            foreach (var i in orphanSaleId)
                Console.WriteLine(
                    $"   ItemId={i.Id}, SaleId={i.SaleId}, ProductId={i.ProductId}, Qty={i.Quantity}"
                );
        }
        else
        {
            Console.WriteLine("✔ No orphan SaleItems.");
        }

        // 2️⃣ Orphan ProductId: ProductId missing in Products table
        Console.WriteLine("\n--- Checking orphan ProductId (ProductId missing) ---");
        var orphanProdId = items.Where(i => !products.Any(p => p.Id == i.ProductId)).ToList();

        if (orphanProdId.Any())
        {
            Console.WriteLine("❌ Found orphan ProductId references:");
            foreach (var i in orphanProdId)
                Console.WriteLine($"   ItemId={i.Id}, ProductId={i.ProductId}, SaleId={i.SaleId}");
        }
        else
        {
            Console.WriteLine("✔ All ProductId references valid.");
        }

        // 3️⃣ SaleItems where SaleId == 0
        Console.WriteLine("\n--- Checking SaleItems with SaleId = 0 ---");
        var zeroSaleId = items.Where(i => i.SaleId == 0).ToList();

        if (zeroSaleId.Any())
        {
            Console.WriteLine("❌ Found items with SaleId = 0:");
            foreach (var i in zeroSaleId)
                Console.WriteLine($"   ItemId={i.Id}, ProductId={i.ProductId}, Qty={i.Quantity}");
        }
        else
        {
            Console.WriteLine("✔ No SaleId=0 items.");
        }

        // 4️⃣ Items for non-existing product (should NEVER happen offline)
        Console.WriteLine("\n--- Checking ProductId = 0 or null ---");
        var zeroProductId = items.Where(i => i.ProductId == 0).ToList();

        if (zeroProductId.Any())
        {
            Console.WriteLine("❌ Found items with ProductId = 0:");
            foreach (var i in zeroProductId)
                Console.WriteLine($"   ItemId={i.Id}, SaleId={i.SaleId}, Qty={i.Quantity}");
        }
        else
        {
            Console.WriteLine("✔ No ProductId=0 items.");
        }

        // 5️⃣ Sales with zero items
        Console.WriteLine("\n--- Checking sales with NO items ---");
        var emptySales = sales.Where(s => !items.Any(i => i.SaleId == s.Id)).ToList();

        if (emptySales.Any())
        {
            Console.WriteLine("❌ Found sales with NO items:");
            foreach (var s in emptySales)
                Console.WriteLine($"   SaleId={s.Id}, Timestamp={s.Timestamp}");
        }
        else
        {
            Console.WriteLine("✔ All sales have items.");
        }

        Console.WriteLine("\n🎯 Integrity check DONE.");
    }
}
