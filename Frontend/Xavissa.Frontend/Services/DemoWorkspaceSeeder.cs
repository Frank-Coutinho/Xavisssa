using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Services;

public class DemoWorkspaceSeeder : IDemoWorkspaceSeeder
{
    private readonly IDbContextFactory<LocalDbContext> _factory;
    private readonly IWorkspaceService _workspace;

    public DemoWorkspaceSeeder(IDbContextFactory<LocalDbContext> factory, IWorkspaceService workspace)
    {
        _factory = factory;
        _workspace = workspace;
    }

    public async Task SeedAsync(StartDemoSessionResponse response)
    {
        _workspace.UseDemoWorkspace();

        await using var db = _factory.CreateDbContext();
        await db.EnsureLocalSchemaAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        foreach (var table in new[]
        {
            "SalePayments", "SaleItems", "Sales", "StockMovements", "StockLevels",
            "ProductVariants", "SellableVariants", "ProductStoreAssignments",
            "Products", "Categories", "Stores", "OfflineIdentities", "SyncLogs", "SyncCursors",
        })
        {
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM {table};");
        }

        var tenantId = response.TenantId ?? 1000001;
        var now = DateTime.UtcNow;

        var stores = new[]
        {
            new StoreRecord { Id = 101, TenantId = tenantId, Name = "Loja Central", Code = "DEMO-CENTRAL", IsActive = true },
            new StoreRecord { Id = 102, TenantId = tenantId, Name = "Loja Bairro", Code = "DEMO-BAIRRO", IsActive = true },
        };
        db.Stores.AddRange(stores);

        var categories = new[]
        {
            new CatalogCategory { Id = 201, TenantId = tenantId, Name = "Bebidas", IsActive = true, ProductCount = 1, CreatedAt = now, UpdatedAt = now },
            new CatalogCategory { Id = 202, TenantId = tenantId, Name = "Mercearia", IsActive = true, ProductCount = 3, CreatedAt = now, UpdatedAt = now },
            new CatalogCategory { Id = 203, TenantId = tenantId, Name = "Higiene", IsActive = true, ProductCount = 1, CreatedAt = now, UpdatedAt = now },
            new CatalogCategory { Id = 204, TenantId = tenantId, Name = "Electrónicos", IsActive = true, ProductCount = 2, CreatedAt = now, UpdatedAt = now },
        };
        db.Categories.AddRange(categories);

        var products = new[]
        {
            Product(301, tenantId, 201, "Água 500ml", "Bebidas", "Bebida", 35m, 210, "DEMO-AGUA"),
            Product(302, tenantId, 202, "Arroz 5kg", "Mercearia", "Alimento", 475m, 74, "DEMO-ARROZ"),
            Product(303, tenantId, 202, "Açúcar 1kg", "Mercearia", "Alimento", 72m, 120, "DEMO-ACUCAR"),
            Product(304, tenantId, 202, "Óleo 1L", "Mercearia", "Alimento", 165m, 86, "DEMO-OLEO"),
            Product(305, tenantId, 203, "Sabão", "Higiene", "Casa", 55m, 96, "DEMO-SABAO"),
            Product(306, tenantId, 204, "Carregador USB", "Electrónicos", "Acessórios", 390m, 35, "DEMO-USB"),
            Product(307, tenantId, 204, "Auscultadores", "Electrónicos", "Acessórios", 650m, 28, "DEMO-AUDIO"),
        };
        db.Products.AddRange(products);

        var assignments = new List<ProductStoreAssignment>();
        var variants = new List<ProductVariantRecord>();
        var sellable = new List<SellableVariantSnapshot>();
        var stockLevels = new List<StockLevel>();
        var stockMovements = new List<StockMovement>();

        var assignmentId = 401;
        var variantId = 501;
        foreach (var product in products)
        {
            foreach (var store in stores)
            {
                var storeMultiplier = store.Id == 101 ? 1m : 1.03m;
                var quantity = store.Id == 101 ? product.StockQuantity : Math.Max(8, product.StockQuantity / 2);
                var price = decimal.Round(product.Price * storeMultiplier, 2);
                var assignment = new ProductStoreAssignment
                {
                    Id = assignmentId++,
                    ProductId = product.Id,
                    TenantId = tenantId,
                    StoreId = store.Id,
                    StoreName = store.Name,
                    Price = price,
                    StockQuantity = quantity,
                    IsActive = true,
                    VariantCount = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                var variant = new ProductVariantRecord
                {
                    Id = variantId++,
                    ProductId = product.Id,
                    AssignmentId = assignment.Id,
                    TenantId = tenantId,
                    StoreId = store.Id,
                    StoreName = store.Name,
                    Name = product.Name,
                    Label = "Sample Data",
                    SKU = $"{product.Code}-{store.Code}",
                    Barcode = $"258{product.Id}{store.Id}",
                    Price = price,
                    CostPrice = decimal.Round(price * 0.7m, 2),
                    StockQuantity = quantity,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                assignments.Add(assignment);
                variants.Add(variant);
                sellable.Add(new SellableVariantSnapshot
                {
                    VariantId = variant.Id,
                    StoreProductId = assignment.Id,
                    ProductId = product.Id,
                    TenantId = tenantId,
                    StoreId = store.Id,
                    ProductName = product.Name,
                    VariantLabel = variant.Label,
                    Barcode = variant.Barcode,
                    SKU = variant.SKU,
                    Price = price,
                    QuantityOnHand = quantity,
                    IsSellable = true,
                    UpdatedAt = now,
                });
                stockLevels.Add(new StockLevel
                {
                    Id = 700 + variant.Id,
                    TenantId = tenantId,
                    StoreId = store.Id,
                    VariantId = variant.Id,
                    QuantityOnHand = quantity,
                    ReorderLevel = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                stockMovements.Add(new StockMovement
                {
                    Id = 900 + variant.Id,
                    TenantId = tenantId,
                    StoreId = store.Id,
                    VariantId = variant.Id,
                    Quantity = quantity,
                    MovementType = "DemoOpeningStock",
                    ReferenceType = "DemoSeed",
                    Notes = "Sample data - opening stock for demo workspace.",
                    CreatedAt = now.AddDays(-6),
                    UpdatedAt = now.AddDays(-6),
                });
            }
        }

        db.ProductStoreAssignments.AddRange(assignments);
        db.ProductVariants.AddRange(variants);
        db.SellableVariants.AddRange(sellable);
        db.StockLevels.AddRange(stockLevels);
        db.StockMovements.AddRange(stockMovements);

        SeedSales(db, tenantId, now, sellable);
        SeedDemoIdentity(db, tenantId, stores);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static Product Product(int id, int tenantId, int categoryId, string name, string category, string brand, decimal price, int stock, string code) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = name,
            Category = category,
            Brand = brand,
            Code = code,
            Description = $"Sample Data - {name}",
            Price = price,
            StockQuantity = stock,
            IsActive = true,
            VariantCount = 2,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow,
        };

    private static void SeedSales(LocalDbContext db, int tenantId, DateTime now, IReadOnlyList<SellableVariantSnapshot> variants)
    {
        var saleId = 1001;
        foreach (var dayOffset in new[] { -5, -3, -1 })
        {
            var storeId = dayOffset == -3 ? 102 : 101;
            var storeVariants = variants.Where(x => x.StoreId == storeId).Take(3).ToList();
            var sale = new Sale
            {
                Id = saleId++,
                TenantId = tenantId,
                StoreId = storeId,
                Timestamp = now.AddDays(dayOffset).AddHours(10),
                ReceiptNumber = $"DEMO-{saleId:0000}",
                PaymentSummary = dayOffset == -1 ? "MobilePayment" : "Cash",
                PaymentStatus = "Paid",
                Synced = true,
                CreatedAt = now.AddDays(dayOffset).AddHours(10),
                UpdatedAt = now.AddDays(dayOffset).AddHours(10),
            };

            foreach (var variant in storeVariants)
            {
                sale.Items.Add(new SaleItem
                {
                    TenantId = tenantId,
                    StoreId = storeId,
                    ProductId = variant.ProductId,
                    VariantId = variant.VariantId,
                    ProductName = variant.ProductName,
                    ProductCategory = "Sample Data",
                    Quantity = 1,
                    UnitPrice = variant.Price,
                    Subtotal = variant.Price,
                    CreatedAt = sale.Timestamp,
                    UpdatedAt = sale.Timestamp,
                });
            }

            sale.TotalAmount = sale.Items.Sum(x => x.Subtotal);
            sale.TotalPaid = sale.TotalAmount;
            sale.Payments.Add(new SalePayment
            {
                TenantId = tenantId,
                StoreId = storeId,
                PaymentMethod = sale.PaymentSummary,
                Amount = sale.TotalAmount,
                CreatedAt = sale.Timestamp,
            });
            db.Sales.Add(sale);
        }
    }

    private static void SeedDemoIdentity(LocalDbContext db, int tenantId, IReadOnlyList<StoreRecord> stores)
    {
        var assignedStores = stores.Select(store => new AssignedStore
        {
            Id = store.Id,
            TenantId = tenantId,
            Name = store.Name,
            Role = AppRoles.StoreManager,
            StoreRoleCode = AppRoles.StoreManager,
        }).ToList();

        var assignedTenants = new[]
        {
            new AssignedTenant
            {
                Id = tenantId,
                Name = "Loja Demo Xavissa",
                Role = AppRoles.TenantAdmin,
                TenantRoleCode = AppRoles.TenantAdmin,
            },
        };

        db.OfflineIdentities.Add(new OfflineIdentity
        {
            Id = 1,
            OnlineUserId = 1000001,
            Username = "demo-admin",
            PasswordHash = string.Empty,
            PlatformRoleCode = AppRoles.User,
            PlatformRole = AppRoles.User,
            ActingRole = AppRoles.StoreManager,
            Role = AppRoles.StoreManager,
            AllowedTenantsJson = JsonSerializer.Serialize(assignedTenants),
            AllowedStoresJson = JsonSerializer.Serialize(assignedStores),
            SelectedTenantId = tenantId,
            SelectedStoreId = assignedStores[0].Id,
            LastOnlineLogin = DateTime.UtcNow,
            IsActive = true,
        });
    }
}
