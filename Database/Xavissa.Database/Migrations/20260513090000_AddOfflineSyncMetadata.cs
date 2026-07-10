using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    public partial class AddOfflineSyncMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var syncTables = new[]
            {
                "Categories",
                "Products",
                "StoreProducts",
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

            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            foreach (var table in syncTables)
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE ""{table}""
                        ADD COLUMN IF NOT EXISTS ""SyncId"" uuid,
                        ADD COLUMN IF NOT EXISTS ""SourceDeviceId"" text,
                        ADD COLUMN IF NOT EXISTS ""ClientCreatedAt"" timestamptz,
                        ADD COLUMN IF NOT EXISTS ""ClientUpdatedAt"" timestamptz,
                        ADD COLUMN IF NOT EXISTS ""LastSyncedAt"" timestamptz;

                    UPDATE ""{table}""
                    SET ""SyncId"" = gen_random_uuid()
                    WHERE ""SyncId"" IS NULL;

                    ALTER TABLE ""{table}""
                        ALTER COLUMN ""SyncId"" SET NOT NULL,
                        ALTER COLUMN ""SyncId"" SET DEFAULT gen_random_uuid();

                    CREATE UNIQUE INDEX IF NOT EXISTS ""UX_{table}_SyncId""
                        ON ""{table}"" (""SyncId"");");
            }

            migrationBuilder.Sql(@"
                ALTER TABLE ""Products""
                    ALTER COLUMN ""Description"" DROP NOT NULL;

                ALTER TABLE ""ProductVariants""
                    ADD COLUMN IF NOT EXISTS ""Description"" text;

                ALTER TABLE ""StockTransfers""
                    DROP CONSTRAINT IF EXISTS ""CK_StockTransfers_DifferentStores"";
                ALTER TABLE ""StockTransfers""
                    ADD CONSTRAINT ""CK_StockTransfers_DifferentStores""
                    CHECK (""FromStoreId"" <> ""ToStoreId"");

                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_Roles_Scope_Code""
                    ON ""Roles"" (""Scope"", ""Code"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""UX_StoreProducts_TenantId_StoreId_ProductId""
                    ON ""StoreProducts"" (""TenantId"", ""StoreId"", ""ProductId"");
                CREATE INDEX IF NOT EXISTS ""IX_ProductVariants_StoreProductId""
                    ON ""ProductVariants"" (""StoreProductId"");
                CREATE INDEX IF NOT EXISTS ""IX_SalePayments_SaleId""
                    ON ""SalePayments"" (""SaleId"");
                CREATE INDEX IF NOT EXISTS ""IX_CashRegisterCashMovements_CashRegisterSessionId""
                    ON ""CashRegisterCashMovements"" (""CashRegisterSessionId"");
                CREATE INDEX IF NOT EXISTS ""IX_StockAdjustmentItems_StockAdjustmentId""
                    ON ""StockAdjustmentItems"" (""StockAdjustmentId"");
                CREATE INDEX IF NOT EXISTS ""IX_StockTransferItems_StockTransferId""
                    ON ""StockTransferItems"" (""StockTransferId"");");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""UX_Roles_Scope_Code"";
                ALTER TABLE ""StockTransfers"" DROP CONSTRAINT IF EXISTS ""CK_StockTransfers_DifferentStores"";
            ");
        }
    }
}
