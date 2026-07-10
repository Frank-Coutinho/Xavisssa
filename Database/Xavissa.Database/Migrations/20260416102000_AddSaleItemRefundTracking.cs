using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Database.Migrations
{
    public partial class AddSaleItemRefundTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRefunded",
                table: "SaleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RefundedQuantity",
                table: "SaleItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "SaleItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefunded",
                table: "DeletedSaleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RefundedQuantity",
                table: "DeletedSaleItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "DeletedSaleItems",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRefunded",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RefundedQuantity",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsRefunded",
                table: "DeletedSaleItems");

            migrationBuilder.DropColumn(
                name: "RefundedQuantity",
                table: "DeletedSaleItems");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "DeletedSaleItems");
        }
    }
}
