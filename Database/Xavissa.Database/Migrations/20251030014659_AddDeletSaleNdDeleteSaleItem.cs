using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Xavissa.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletSaleNdDeleteSaleItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeletedSales_Users_UserId",
                table: "DeletedSales");

            migrationBuilder.DropIndex(
                name: "IX_DeletedSales_UserId",
                table: "DeletedSales");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "DeletedSales",
                newName: "ReceiptNumber");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "DeletedSales",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "DeletedSales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "DeletedSales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeletedByUserId",
                table: "DeletedSales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "DeletedSales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "DeletedSales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleDate",
                table: "DeletedSales",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "DeletedSales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DeletedSaleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeletedSaleId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductCategory = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedSaleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeletedSaleItems_DeletedSales_DeletedSaleId",
                        column: x => x.DeletedSaleId,
                        principalTable: "DeletedSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedSaleItems_DeletedSaleId",
                table: "DeletedSaleItems",
                column: "DeletedSaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedSaleItems");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "SaleDate",
                table: "DeletedSales");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "DeletedSales");

            migrationBuilder.RenameColumn(
                name: "ReceiptNumber",
                table: "DeletedSales",
                newName: "Reason");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "DeletedSales",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedSales_UserId",
                table: "DeletedSales",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeletedSales_Users_UserId",
                table: "DeletedSales",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
