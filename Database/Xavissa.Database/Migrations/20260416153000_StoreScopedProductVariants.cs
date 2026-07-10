using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    public partial class StoreScopedProductVariants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "ProductVariants",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductStoreAssignmentId",
                table: "ProductVariants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreId",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                INSERT INTO ""ProductStoreAssignments"" (""TenantId"", ""ProductId"", ""StoreId"", ""Price"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"", ""CreatedBy"", ""UpdatedBy"")
                SELECT DISTINCT
                    sl.""TenantId"",
                    pv.""ProductId"",
                    sl.""StoreId"",
                    COALESCE(pv.""Price"", 0),
                    TRUE,
                    NOW(),
                    NOW(),
                    sl.""UpdatedBy"",
                    sl.""UpdatedBy""
                FROM ""StockLevels"" sl
                INNER JOIN ""ProductVariants"" pv ON pv.""Id"" = sl.""VariantId""
                LEFT JOIN ""ProductStoreAssignments"" psa
                    ON psa.""ProductId"" = pv.""ProductId""
                    AND psa.""StoreId"" = sl.""StoreId""
                WHERE psa.""Id"" IS NULL;
            ");

            migrationBuilder.Sql(@"
                WITH first_assignments AS (
                    SELECT DISTINCT ON (psa.""ProductId"")
                        psa.""ProductId"",
                        psa.""Id"" AS ""AssignmentId"",
                        psa.""StoreId"",
                        psa.""Price""
                    FROM ""ProductStoreAssignments"" psa
                    ORDER BY psa.""ProductId"", psa.""Id""
                )
                UPDATE ""ProductVariants"" pv
                SET
                    ""StoreId"" = fa.""StoreId"",
                    ""ProductStoreAssignmentId"" = fa.""AssignmentId"",
                    ""Price"" = COALESCE(pv.""Price"", fa.""Price"")
                FROM first_assignments fa
                WHERE pv.""ProductId"" = fa.""ProductId""
                  AND pv.""StoreId"" = 0;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""ProductVariants""
                    (""ProductId"", ""ProductStoreAssignmentId"", ""StoreId"", ""SKU"", ""Barcode"", ""Price"", ""CostPrice"", ""IsActive"", ""CreatedAt"", ""LastModified"", ""CreatedBy"", ""UpdatedBy"", ""TenantId"", ""Label"", ""AttributesJson"")
                SELECT
                    template.""ProductId"",
                    psa.""Id"",
                    psa.""StoreId"",
                    template.""SKU"",
                    NULL,
                    COALESCE(psa.""Price"", template.""Price"", 0),
                    template.""CostPrice"",
                    psa.""IsActive"",
                    NOW(),
                    NOW(),
                    template.""CreatedBy"",
                    template.""UpdatedBy"",
                    template.""TenantId"",
                    template.""Label"",
                    template.""AttributesJson""
                FROM ""ProductStoreAssignments"" psa
                INNER JOIN LATERAL (
                    SELECT pv.*
                    FROM ""ProductVariants"" pv
                    WHERE pv.""ProductId"" = psa.""ProductId""
                    ORDER BY pv.""Id""
                    LIMIT 1
                ) AS template ON TRUE
                LEFT JOIN ""ProductVariants"" existing
                    ON existing.""ProductStoreAssignmentId"" = psa.""Id""
                WHERE existing.""Id"" IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductStoreAssignmentId",
                table: "ProductVariants",
                column: "ProductStoreAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_StoreId",
                table: "ProductVariants",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_ProductStoreAssignments_ProductStoreAssignmentId",
                table: "ProductVariants",
                column: "ProductStoreAssignmentId",
                principalTable: "ProductStoreAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_ProductStoreAssignments_ProductStoreAssignmentId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductStoreAssignmentId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_StoreId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "ProductStoreAssignmentId",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ProductVariants");
        }
    }
}
