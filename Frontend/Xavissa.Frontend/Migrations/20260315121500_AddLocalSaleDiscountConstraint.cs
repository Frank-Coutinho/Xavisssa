using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Frontend.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalSaleDiscountConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "Sales_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Sales" PRIMARY KEY AUTOINCREMENT,
                    "SaleDate" TEXT NOT NULL,
                    "TotalAmount" TEXT NOT NULL,
                    "PaymentMethod" INTEGER NOT NULL,
                    "Discount" TEXT NULL,
                    "AmountPaid" TEXT NULL,
                    "ReceiptNumber" TEXT NOT NULL,
                    "IsRefunded" INTEGER NOT NULL,
                    "RefundReason" TEXT NULL,
                    "Synced" INTEGER NOT NULL,
                    "SyncFailed" INTEGER NOT NULL,
                    CONSTRAINT "CK_Sales_Discount_NotGreaterThanTotalAmount"
                        CHECK ("Discount" IS NULL OR "Discount" <= "TotalAmount")
                );
                """
            );

            migrationBuilder.Sql(
                """
                INSERT INTO "Sales_new"
                    ("Id", "SaleDate", "TotalAmount", "PaymentMethod", "Discount", "AmountPaid",
                     "ReceiptNumber", "IsRefunded", "RefundReason", "Synced", "SyncFailed")
                SELECT
                    "Id",
                    "SaleDate",
                    "TotalAmount",
                    "PaymentMethod",
                    CASE
                        WHEN "Discount" IS NULL THEN NULL
                        WHEN "Discount" > "TotalAmount" THEN "TotalAmount"
                        ELSE "Discount"
                    END,
                    "AmountPaid",
                    "ReceiptNumber",
                    "IsRefunded",
                    "RefundReason",
                    "Synced",
                    "SyncFailed"
                FROM "Sales";
                """
            );

            migrationBuilder.Sql(@"DROP TABLE ""Sales"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Sales_new"" RENAME TO ""Sales"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE "Sales_old" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Sales" PRIMARY KEY AUTOINCREMENT,
                    "SaleDate" TEXT NOT NULL,
                    "TotalAmount" TEXT NOT NULL,
                    "PaymentMethod" INTEGER NOT NULL,
                    "Discount" TEXT NULL,
                    "AmountPaid" TEXT NULL,
                    "ReceiptNumber" TEXT NOT NULL,
                    "IsRefunded" INTEGER NOT NULL,
                    "RefundReason" TEXT NULL,
                    "Synced" INTEGER NOT NULL,
                    "SyncFailed" INTEGER NOT NULL
                );
                """
            );

            migrationBuilder.Sql(
                """
                INSERT INTO "Sales_old"
                    ("Id", "SaleDate", "TotalAmount", "PaymentMethod", "Discount", "AmountPaid",
                     "ReceiptNumber", "IsRefunded", "RefundReason", "Synced", "SyncFailed")
                SELECT
                    "Id",
                    "SaleDate",
                    "TotalAmount",
                    "PaymentMethod",
                    "Discount",
                    "AmountPaid",
                    "ReceiptNumber",
                    "IsRefunded",
                    "RefundReason",
                    "Synced",
                    "SyncFailed"
                FROM "Sales";
                """
            );

            migrationBuilder.Sql(@"DROP TABLE ""Sales"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Sales_old"" RENAME TO ""Sales"";");
        }
    }
}
