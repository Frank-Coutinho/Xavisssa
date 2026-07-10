using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleDiscountConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Sales"
                SET "Discount" = "TotalAmount"
                WHERE "Discount" IS NOT NULL AND "Discount" > "TotalAmount";
                """
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Discount_NotGreaterThanTotalAmount",
                table: "Sales",
                sql: "\"Discount\" IS NULL OR \"Discount\" <= \"TotalAmount\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Discount_NotGreaterThanTotalAmount",
                table: "Sales");
        }
    }
}
