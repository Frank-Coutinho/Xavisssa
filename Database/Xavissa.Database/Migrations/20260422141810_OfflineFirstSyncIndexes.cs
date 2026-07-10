using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    /// <inheritdoc />
    public partial class OfflineFirstSyncIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Products_TenantId_LastModified"
                ON "Products" ("TenantId", "LastModified");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_ProductVariants_TenantId_LastModified"
                ON "ProductVariants" ("TenantId", "LastModified");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_StoreProducts_TenantId_StoreId_UpdatedAt"
                ON "StoreProducts" ("TenantId", "StoreId", "UpdatedAt");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Sales_TenantId_StoreId_LastModified"
                ON "Sales" ("TenantId", "StoreId", "LastModified");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_SaleItems_StoreId_LastModified"
                ON "SaleItems" ("StoreId", "LastModified");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_StockLevels_StoreId_UpdatedAt"
                ON "StockLevels" ("StoreId", "UpdatedAt");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_StoreId_UpdatedAt"
                ON "StockMovements" ("StoreId", "UpdatedAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_StockMovements_StoreId_UpdatedAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_StockLevels_StoreId_UpdatedAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_SaleItems_StoreId_LastModified";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Sales_TenantId_StoreId_LastModified";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_StoreProducts_TenantId_StoreId_UpdatedAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_ProductVariants_TenantId_LastModified";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Products_TenantId_LastModified";""");
        }
    }
}
