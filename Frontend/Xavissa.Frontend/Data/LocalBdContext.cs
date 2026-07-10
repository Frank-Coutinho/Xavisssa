using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Data
{
    public class LocalDbContext : DbContext
    {
        private const string SeedUserPasswordHash =
            "AAECAwQFBgcICQoLDA0ODwO3dv0fC0LEPbo863rg/aACz/TqP19+lu3P8R1Mu0Ya";

        private static readonly SemaphoreSlim SchemaLock = new(1, 1);
        private readonly List<SyncLog> _pendingLogs = new();

        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }
        public LocalDbContext() { }

        public static string GetLocalDbPath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Xavissa",
                "Workspaces",
                "Real");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "xavissa.db");
        }

        public static string BuildConnectionString(string dbPath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true,
                Pooling = true,
            };

            return builder.ToString();
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<StoreRecord> Stores => Set<StoreRecord>();
        public DbSet<CatalogCategory> Categories => Set<CatalogCategory>();
        public DbSet<ProductStoreAssignment> ProductStoreAssignments => Set<ProductStoreAssignment>();
        public DbSet<ProductVariantRecord> ProductVariants => Set<ProductVariantRecord>();
        public DbSet<SellableVariantSnapshot> SellableVariants => Set<SellableVariantSnapshot>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();
        public DbSet<SalePayment> SalePayments => Set<SalePayment>();
        public DbSet<StockLevel> StockLevels => Set<StockLevel>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>();
        public DbSet<CashRegisterCashMovement> CashRegisterCashMovements => Set<CashRegisterCashMovement>();
        public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
        public DbSet<StockAdjustmentItem> StockAdjustmentItems => Set<StockAdjustmentItem>();
        public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
        public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
        public DbSet<SyncCursor> SyncCursors => Set<SyncCursor>();
        public DbSet<LocalSchemaInfo> LocalSchemaInfo => Set<LocalSchemaInfo>();
        public DbSet<OfflineIdentity> OfflineIdentities => Set<OfflineIdentity>();
        public DbSet<LocalDeviceIdentity> LocalDeviceIdentities => Set<LocalDeviceIdentity>();
        public DbSet<LocalLicenseSnapshot> LocalLicenseSnapshots => Set<LocalLicenseSnapshot>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseSqlite(BuildConnectionString(GetLocalDbPath()));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.Property(s => s.Timestamp).HasColumnName("SaleDate");
                entity.HasIndex(s => s.SyncId).IsUnique();
                entity.HasMany(s => s.Items)
                    .WithOne(i => i.Sale)
                    .HasForeignKey(i => i.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(s => s.Payments)
                    .WithOne(p => p.Sale)
                    .HasForeignKey(p => p.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.ToTable(t =>
                    t.HasCheckConstraint("CK_Sales_Discount_NotGreaterThanTotalAmount", "\"Discount\" IS NULL OR \"Discount\" <= \"TotalAmount\""));
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.SyncId).IsUnique();
                entity.Property(p => p.Barcode).IsRequired(false);
                entity.Property(p => p.Category).IsRequired(false);
                entity.Property(p => p.Brand).IsRequired(false);
                entity.Property(p => p.Label).IsRequired(false);
                entity.Property(p => p.AttributesJson).IsRequired(false);
                entity.Property(p => p.Code).IsRequired(false);
                entity.Property(p => p.Color).IsRequired(false);
                entity.Property(p => p.Size).IsRequired(false);
                entity.Property(p => p.SKU).IsRequired(false);
                entity.Property(p => p.Description).IsRequired(false);
                entity.Property(p => p.ImageUrl).IsRequired(false);
            });

            modelBuilder.Entity<StoreRecord>(entity =>
            {
                entity.Property(store => store.Code).IsRequired(false);
            });

            modelBuilder.Entity<CatalogCategory>(entity =>
            {
                entity.HasIndex(category => category.SyncId).IsUnique();
                entity.Property(category => category.Name).IsRequired();
            });

            modelBuilder.Entity<ProductStoreAssignment>(entity =>
            {
                entity.HasIndex(assignment => assignment.SyncId).IsUnique();
                entity.HasIndex(assignment => new { assignment.TenantId, assignment.StoreId, assignment.ProductId }).IsUnique();
                entity.Property(assignment => assignment.StoreName).IsRequired(false);
            });

            modelBuilder.Entity<ProductVariantRecord>(entity =>
            {
                entity.Ignore(variant => variant.StoreProductId);
                entity.HasIndex(variant => variant.SyncId).IsUnique();
                entity.HasIndex(variant => variant.AssignmentId);
                entity.Property(variant => variant.StoreName).IsRequired(false);
                entity.Property(variant => variant.Name).IsRequired(false);
                entity.Property(variant => variant.Label).IsRequired(false);
                entity.Property(variant => variant.SKU).IsRequired(false);
                entity.Property(variant => variant.Barcode).IsRequired(false);
            });

            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.HasIndex(item => item.SyncId).IsUnique();
                entity.HasIndex(item => item.SaleId);
            });

            modelBuilder.Entity<SalePayment>(entity =>
            {
                entity.HasIndex(payment => payment.SyncId).IsUnique();
                entity.HasIndex(payment => payment.SaleId);
            });

            modelBuilder.Entity<StockLevel>(entity =>
            {
                entity.HasIndex(stock => stock.SyncId).IsUnique();
                entity.HasIndex(stock => new { stock.TenantId, stock.StoreId, stock.VariantId }).IsUnique();
            });

            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasIndex(movement => movement.SyncId).IsUnique();
                entity.HasIndex(movement => new { movement.TenantId, movement.StoreId, movement.VariantId });
            });

            modelBuilder.Entity<CashRegisterSession>(entity =>
            {
                entity.HasIndex(session => session.SyncId).IsUnique();
                entity.HasMany(session => session.CashMovements)
                    .WithOne()
                    .HasForeignKey(movement => movement.CashRegisterSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CashRegisterCashMovement>(entity =>
            {
                entity.HasIndex(movement => movement.SyncId).IsUnique();
                entity.HasIndex(movement => movement.CashRegisterSessionId);
            });

            modelBuilder.Entity<StockAdjustment>(entity =>
            {
                entity.HasIndex(adjustment => adjustment.SyncId).IsUnique();
                entity.HasMany(adjustment => adjustment.Items)
                    .WithOne()
                    .HasForeignKey(item => item.StockAdjustmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StockAdjustmentItem>(entity =>
            {
                entity.HasIndex(item => item.SyncId).IsUnique();
                entity.HasIndex(item => item.StockAdjustmentId);
            });

            modelBuilder.Entity<StockTransfer>(entity =>
            {
                entity.HasIndex(transfer => transfer.SyncId).IsUnique();
                entity.HasMany(transfer => transfer.Items)
                    .WithOne()
                    .HasForeignKey(item => item.StockTransferId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StockTransferItem>(entity =>
            {
                entity.HasIndex(item => item.SyncId).IsUnique();
                entity.HasIndex(item => item.StockTransferId);
            });

            modelBuilder.Entity<SellableVariantSnapshot>(entity =>
            {
                entity.ToTable("SellableVariants");
            });

            modelBuilder.Entity<SyncCursor>(entity =>
            {
                entity.ToTable("SyncCursors");
            });

            modelBuilder.Entity<LocalSchemaInfo>(entity =>
            {
                entity.ToTable("LocalSchemaInfo");
            });

            modelBuilder.Entity<OfflineIdentity>().HasData(
                new OfflineIdentity
                {
                    Id = 1,
                    OnlineUserId = 1,
                    Username = "manager",
                    PasswordHash = SeedUserPasswordHash,
                    Role = AppRoles.TenantAdmin,
                    PlatformRoleCode = AppRoles.User,
                    PlatformRole = "User",
                    ActingRole = AppRoles.TenantAdmin,
                    AllowedTenantsJson = "[]",
                    AllowedStoresJson = "[]",
                    LastOnlineLogin = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                });

            modelBuilder.Entity<LocalDeviceIdentity>(entity =>
            {
                entity.ToTable("LocalDeviceIdentity");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedNever();
                entity.Property(x => x.LocalDeviceId).IsRequired();
                entity.Property(x => x.DeviceFingerprint).IsRequired();
            });

            modelBuilder.Entity<LocalLicenseSnapshot>(entity =>
            {
                entity.ToTable("LocalLicenseSnapshots");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).ValueGeneratedNever();
                entity.Property(x => x.Signature).IsRequired();
                entity.Property(x => x.DeviceFingerprint).IsRequired();
            });

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            ApplyOfflineSyncMetadata();
            CaptureEntityChanges();
            if (_pendingLogs.Count > 0)
            {
                SyncLogs.AddRange(_pendingLogs);
                _pendingLogs.Clear();
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyOfflineSyncMetadata();
            CaptureEntityChanges();
            if (_pendingLogs.Count > 0)
            {
                SyncLogs.AddRange(_pendingLogs);
                _pendingLogs.Clear();
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyOfflineSyncMetadata()
        {
            var now = DateTimeOffset.UtcNow;
            var deviceId = $"machine:{Environment.MachineName}";

            foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                var type = entry.Entity.GetType();
                var syncIdProperty = type.GetProperty("SyncId");
                if (syncIdProperty?.PropertyType != typeof(Guid))
                    continue;

                var syncId = (Guid)(syncIdProperty.GetValue(entry.Entity) ?? Guid.Empty);
                if (syncId == Guid.Empty)
                    syncIdProperty.SetValue(entry.Entity, Guid.NewGuid());

                var sourceDeviceProperty = type.GetProperty("SourceDeviceId");
                if (sourceDeviceProperty?.GetValue(entry.Entity) is not string sourceDevice || string.IsNullOrWhiteSpace(sourceDevice))
                    sourceDeviceProperty?.SetValue(entry.Entity, deviceId);

                var clientCreatedProperty = type.GetProperty("ClientCreatedAt");
                var clientUpdatedProperty = type.GetProperty("ClientUpdatedAt");
                if (entry.State == EntityState.Added)
                {
                    if (clientCreatedProperty?.GetValue(entry.Entity) == null)
                        clientCreatedProperty?.SetValue(entry.Entity, now);
                    if (clientUpdatedProperty?.GetValue(entry.Entity) == null)
                        clientUpdatedProperty?.SetValue(entry.Entity, clientCreatedProperty?.GetValue(entry.Entity) ?? now);
                }
                else
                {
                    clientUpdatedProperty?.SetValue(entry.Entity, now);
                }

            }
        }

        private void CaptureEntityChanges()
        {
            var entries = ChangeTracker.Entries().Where(e =>
                e.State != EntityState.Unchanged
                && e.Entity is not SyncLog
                && e.Entity is not SyncCursor
                && e.Entity is not Xavissa.Frontend.Models.LocalSchemaInfo
                && e.Entity is not OfflineIdentity
                && e.Entity is not LocalDeviceIdentity
                && e.Entity is not LocalLicenseSnapshot
                && e.Entity is not Product
                && e.Entity is not StoreRecord
                && e.Entity is not CatalogCategory
                && e.Entity is not ProductStoreAssignment
                && e.Entity is not ProductVariantRecord
                && e.Entity is not SellableVariantSnapshot).ToList();
            foreach (var e in entries)
            {
                var operation = e.State switch
                {
                    EntityState.Added => "Insert",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => e.State.ToString(),
                };

                _pendingLogs.Add(new SyncLog
                {
                    TableName = e.Entity.GetType().Name,
                    Operation = operation,
                    Payload = BuildCompactSyncLogPayload(e.Entity),
                    Timestamp = DateTime.UtcNow,
                    Synced = false,
                });
            }
        }

        private static string BuildCompactSyncLogPayload(object entity)
        {
            var idProperty = entity.GetType().GetProperty("Id");
            var id = idProperty?.GetValue(entity);
            return JsonSerializer.Serialize(new
            {
                entity = entity.GetType().Name,
                id,
            });
        }

        public async Task RunSalesSchemaMigrationAsync()
        {
            await EnsureColumnsAsync("Sales", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["SaleDate"] = "TEXT",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TotalAmount"] = "REAL NOT NULL DEFAULT 0",
                ["Discount"] = "REAL",
                ["TotalPaid"] = "REAL NOT NULL DEFAULT 0",
                ["PaymentSummary"] = "TEXT NOT NULL DEFAULT ''",
                ["PaymentStatus"] = "TEXT NOT NULL DEFAULT 'Paid'",
                ["ChangeGiven"] = "REAL",
                ["ReceiptNumber"] = "TEXT",
                ["IsRefunded"] = "INTEGER NOT NULL DEFAULT 0",
                ["RefundReason"] = "TEXT",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["Synced"] = "INTEGER NOT NULL DEFAULT 0",
                ["SyncFailed"] = "INTEGER NOT NULL DEFAULT 0",
            });
            await EnsureSalesDiscountConstraintAsync();
        }

        public async Task EnsureLocalSchemaAsync()
        {
            await SchemaLock.WaitAsync();
            try
            {
                await EnsureLocalSchemaCoreAsync();
            }
            finally
            {
                SchemaLock.Release();
            }
        }

        private async Task EnsureLocalSchemaCoreAsync()
        {
            await Database.OpenConnectionAsync();
            await Database.ExecuteSqlRawAsync(LocalDbSchema.ConfigureSqlitePragmasSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateLocalSchemaInfoTableSql);

            var currentVersion = await GetLocalSchemaVersionAsync();
            if (currentVersion >= LocalDbSchema.CurrentSchemaVersion)
                return;

            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStoresTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateCategoriesTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateProductsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateProductStoreAssignmentsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateProductVariantsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSellableVariantsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSalesTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSaleItemsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSalePaymentsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockLevelsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockMovementsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateCashRegisterSessionsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateCashRegisterCashMovementsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockAdjustmentsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockAdjustmentItemsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockTransfersTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateStockTransferItemsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateOfflineIdentitiesTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateLocalDeviceIdentityTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateLocalLicenseSnapshotsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSyncLogsTableSql);
            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateSyncCursorsTableSql);

            await EnsureColumnsAsync("Stores", new Dictionary<string, string>
            {
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["Name"] = "TEXT NOT NULL DEFAULT ''",
                ["Code"] = "TEXT NOT NULL DEFAULT ''",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["IsActive"] = "INTEGER NOT NULL DEFAULT 1",
            });
            await EnsureOfflineSyncColumnsAsync("Stores");

            await EnsureColumnsAsync("Categories", new Dictionary<string, string>
            {
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["Name"] = "TEXT NOT NULL DEFAULT ''",
                ["IsActive"] = "INTEGER NOT NULL DEFAULT 1",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["ProductCount"] = "INTEGER NOT NULL DEFAULT 0",
            });
            await EnsureOfflineSyncColumnsAsync("Categories");

            await EnsureColumnsAsync("OfflineIdentities", new Dictionary<string, string>
            {
                ["OnlineUserId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ApiToken"] = "TEXT NOT NULL DEFAULT ''",
                ["Role"] = "TEXT NOT NULL DEFAULT 'User'",
                ["PlatformRoleId"] = "INTEGER",
                ["PlatformRoleCode"] = "TEXT NOT NULL DEFAULT ''",
                ["PlatformRole"] = "TEXT NOT NULL DEFAULT 'User'",
                ["ActingRole"] = "TEXT NOT NULL DEFAULT 'Clerk'",
                ["AllowedTenantsJson"] = "TEXT NOT NULL DEFAULT '[]'",
                ["AllowedStoresJson"] = "TEXT NOT NULL DEFAULT '[]'",
                ["SelectedTenantId"] = "INTEGER",
                ["SelectedStoreId"] = "INTEGER",
            });

            await EnsureColumnsAsync("Products", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["VariantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["AssignmentId"] = "INTEGER NOT NULL DEFAULT 0",
                ["CategoryId"] = "INTEGER",
                ["Barcode"] = "TEXT",
                ["Category"] = "TEXT NOT NULL DEFAULT ''",
                ["Brand"] = "TEXT",
                ["Color"] = "TEXT",
                ["Size"] = "TEXT",
                ["SKU"] = "TEXT",
                ["Label"] = "TEXT",
                ["AttributesJson"] = "TEXT",
                ["CreatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["Price"] = "REAL NOT NULL DEFAULT 0",
                ["StockQuantity"] = "INTEGER NOT NULL DEFAULT 0",
                ["VariantCount"] = "INTEGER NOT NULL DEFAULT 0",
                ["UpdatedAt"] = "TEXT",
            });
            await EnsureOfflineSyncColumnsAsync("Products");
            await NormalizeProductNullTextColumnsAsync();

            await EnsureColumnsAsync("ProductStoreAssignments", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreName"] = "TEXT NOT NULL DEFAULT ''",
                ["Price"] = "REAL NOT NULL DEFAULT 0",
                ["StockQuantity"] = "INTEGER NOT NULL DEFAULT 0",
                ["IsActive"] = "INTEGER NOT NULL DEFAULT 1",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["VariantCount"] = "INTEGER NOT NULL DEFAULT 0",
            });
            await EnsureOfflineSyncColumnsAsync("ProductStoreAssignments");

            await EnsureColumnsAsync("ProductVariants", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductId"] = "INTEGER NOT NULL DEFAULT 0",
                ["AssignmentId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreName"] = "TEXT NOT NULL DEFAULT ''",
                ["Name"] = "TEXT NOT NULL DEFAULT ''",
                ["Description"] = "TEXT",
                ["Label"] = "TEXT NOT NULL DEFAULT ''",
                ["SKU"] = "TEXT NOT NULL DEFAULT ''",
                ["Barcode"] = "TEXT NOT NULL DEFAULT ''",
                ["Price"] = "REAL NOT NULL DEFAULT 0",
                ["CostPrice"] = "REAL",
                ["AttributesJson"] = "TEXT",
                ["StockQuantity"] = "INTEGER NOT NULL DEFAULT 0",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
                ["IsActive"] = "INTEGER NOT NULL DEFAULT 1",
            });
            await EnsureOfflineSyncColumnsAsync("ProductVariants");

            await EnsureColumnsAsync("SellableVariants", new Dictionary<string, string>
            {
                ["StoreProductId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductName"] = "TEXT NOT NULL DEFAULT ''",
                ["VariantLabel"] = "TEXT NOT NULL DEFAULT ''",
                ["Barcode"] = "TEXT NOT NULL DEFAULT ''",
                ["SKU"] = "TEXT NOT NULL DEFAULT ''",
                ["Price"] = "REAL NOT NULL DEFAULT 0",
                ["QuantityOnHand"] = "INTEGER NOT NULL DEFAULT 0",
                ["IsSellable"] = "INTEGER NOT NULL DEFAULT 1",
                ["UpdatedAt"] = "TEXT NOT NULL DEFAULT ''",
            });

            await RunSalesSchemaMigrationAsync();
            await EnsureOfflineSyncColumnsAsync("Sales");

            await EnsureColumnsAsync("SaleItems", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["TenantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["StoreId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductId"] = "INTEGER NOT NULL DEFAULT 0",
                ["VariantId"] = "INTEGER NOT NULL DEFAULT 0",
                ["ProductName"] = "TEXT",
                ["ProductCategory"] = "TEXT",
                ["Subtotal"] = "REAL NOT NULL DEFAULT 0",
                ["IsRefunded"] = "INTEGER NOT NULL DEFAULT 0",
                ["RefundedQuantity"] = "INTEGER NOT NULL DEFAULT 0",
                ["RefundReason"] = "TEXT",
                ["RefundedAt"] = "TEXT",
                ["RefundedByUserId"] = "INTEGER",
                ["UpdatedBy"] = "INTEGER",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
            });
            await EnsureOfflineSyncColumnsAsync("SaleItems");

            await EnsureColumnsAsync("SalePayments", new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["UpdatedAt"] = "TEXT",
                ["DeletedAt"] = "TEXT",
            });
            await EnsureOfflineSyncColumnsAsync("SalePayments");

            foreach (var tableName in new[]
            {
                "StockLevels",
                "StockMovements",
                "CashRegisterSessions",
                "CashRegisterCashMovements",
                "StockAdjustments",
                "StockAdjustmentItems",
                "StockTransfers",
                "StockTransferItems",
            })
            {
                await EnsureOfflineSyncColumnsAsync(tableName);
            }

            await BackfillOfflineSyncMetadataAsync();

            await Database.ExecuteSqlRawAsync(LocalDbSchema.CreateOperationalIndexesSql);
            await SetLocalSchemaVersionAsync(LocalDbSchema.CurrentSchemaVersion);
        }

        private async Task<int> GetLocalSchemaVersionAsync()
        {
            var conn = Database.GetDbConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Version FROM LocalSchemaInfo WHERE Id = 1;";
            var value = await cmd.ExecuteScalarAsync();
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private async Task SetLocalSchemaVersionAsync(int version)
        {
            await Database.ExecuteSqlRawAsync(
                @"INSERT INTO LocalSchemaInfo (Id, Version, UpdatedAt)
                  VALUES (1, {0}, {1})
                  ON CONFLICT(Id) DO UPDATE SET Version = excluded.Version, UpdatedAt = excluded.UpdatedAt;",
                version,
                DateTime.UtcNow);
        }

        private async Task EnsureColumnsAsync(string tableName, Dictionary<string, string> neededColumns)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var conn = Database.GetDbConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                existingColumns.Add(reader.GetString(1));

            foreach (var kv in neededColumns)
            {
                if (!existingColumns.Contains(kv.Key))
                    await Database.ExecuteSqlRawAsync($"ALTER TABLE {tableName} ADD COLUMN {kv.Key} {kv.Value};");
            }
        }

        private async Task EnsureOfflineSyncColumnsAsync(string tableName)
        {
            await EnsureColumnsAsync(tableName, new Dictionary<string, string>
            {
                ["OnlineId"] = "INTEGER NOT NULL DEFAULT 0",
                ["SyncId"] = "TEXT NOT NULL DEFAULT ''",
                ["SourceDeviceId"] = "TEXT",
                ["ClientCreatedAt"] = "TEXT",
                ["ClientUpdatedAt"] = "TEXT",
                ["LastSyncedAt"] = "TEXT",
                ["CreatedAt"] = "TEXT",
                ["UpdatedAt"] = "TEXT",
            });
        }

        private async Task BackfillOfflineSyncMetadataAsync()
        {
            var deviceId = $"machine:{Environment.MachineName}";
            var syncTables = new[]
            {
                "Stores",
                "Categories",
                "Products",
                "ProductStoreAssignments",
                "ProductVariants",
                "Sales",
                "SaleItems",
                "SalePayments",
                "StockLevels",
                "StockMovements",
                "CashRegisterSessions",
                "CashRegisterCashMovements",
                "StockAdjustments",
                "StockAdjustmentItems",
                "StockTransfers",
                "StockTransferItems",
            };

            foreach (var tableName in syncTables)
            {
                await Database.ExecuteSqlRawAsync($@"
                    UPDATE {tableName}
                    SET SyncId = lower(
                        hex(randomblob(4)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        '4' || substr(hex(randomblob(2)), 2) || '-' ||
                        substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)), 2) || '-' ||
                        hex(randomblob(6))
                    )
                    WHERE SyncId IS NULL
                       OR SyncId = ''
                       OR SyncId = '00000000-0000-0000-0000-000000000000';");

                await Database.ExecuteSqlRawAsync($@"
                    UPDATE {tableName}
                    SET
                        SourceDeviceId = COALESCE(NULLIF(SourceDeviceId, ''), {{0}}),
                        ClientCreatedAt = COALESCE(ClientCreatedAt, CreatedAt, UpdatedAt, datetime('now')),
                        ClientUpdatedAt = COALESCE(ClientUpdatedAt, UpdatedAt, CreatedAt, datetime('now'))
                    WHERE SourceDeviceId IS NULL
                       OR SourceDeviceId = ''
                       OR ClientCreatedAt IS NULL
                       OR ClientUpdatedAt IS NULL;",
                    deviceId);
            }
        }

        private async Task EnsureSalesDiscountConstraintAsync()
        {
            var conn = Database.GetDbConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Sales';";
            var createSql = (await cmd.ExecuteScalarAsync())?.ToString() ?? string.Empty;
            if (createSql.Contains("CK_Sales_Discount_NotGreaterThanTotalAmount"))
                return;

            await Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Sales_new (
                    Id INTEGER PRIMARY KEY,
                    SaleDate TEXT NOT NULL,
                    TenantId INTEGER NOT NULL DEFAULT 0,
                    StoreId INTEGER NOT NULL DEFAULT 0,
                    TotalAmount REAL NOT NULL,
                    Discount REAL,
                    TotalPaid REAL NOT NULL DEFAULT 0,
                    PaymentSummary TEXT NOT NULL DEFAULT '',
                    PaymentStatus TEXT NOT NULL DEFAULT 'Paid',
                    ChangeGiven REAL,
                    ReceiptNumber TEXT,
                    IsRefunded INTEGER NOT NULL DEFAULT 0,
                    RefundReason TEXT,
                    UpdatedAt TEXT,
                    Synced INTEGER NOT NULL DEFAULT 0,
                    SyncFailed INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT CK_Sales_Discount_NotGreaterThanTotalAmount CHECK (Discount IS NULL OR Discount <= TotalAmount)
                );");

            await Database.ExecuteSqlRawAsync(@"
                INSERT INTO Sales_new
                    (Id, SaleDate, TenantId, StoreId, TotalAmount, Discount, TotalPaid, PaymentSummary, PaymentStatus, ChangeGiven, ReceiptNumber, IsRefunded, RefundReason, UpdatedAt, Synced, SyncFailed)
                SELECT
                    Id,
                    SaleDate,
                    COALESCE(TenantId, 0),
                    COALESCE(StoreId, 0),
                    TotalAmount,
                    CASE WHEN Discount IS NULL THEN NULL WHEN Discount > TotalAmount THEN TotalAmount ELSE Discount END,
                    COALESCE(TotalPaid, TotalAmount, 0),
                    COALESCE(PaymentSummary, ''),
                    COALESCE(PaymentStatus, 'Paid'),
                    ChangeGiven,
                    ReceiptNumber,
                    IsRefunded,
                    RefundReason,
                    UpdatedAt,
                    Synced,
                    SyncFailed
                FROM Sales;");

            await Database.ExecuteSqlRawAsync("DROP TABLE Sales;");
            await Database.ExecuteSqlRawAsync("ALTER TABLE Sales_new RENAME TO Sales;");
        }

        private async Task NormalizeProductNullTextColumnsAsync()
        {
            await Database.ExecuteSqlRawAsync(@"
                UPDATE Products
                SET
                    Name = COALESCE(Name, ''),
                    Barcode = COALESCE(Barcode, ''),
                    Category = COALESCE(Category, ''),
                    Brand = COALESCE(Brand, ''),
                    Label = COALESCE(Label, ''),
                    AttributesJson = COALESCE(AttributesJson, ''),
                    Code = COALESCE(Code, ''),
                    Color = COALESCE(Color, ''),
                    Size = COALESCE(Size, ''),
                    SKU = COALESCE(SKU, ''),
                    ImageUrl = COALESCE(ImageUrl, '');
            ");
        }
    }
}
