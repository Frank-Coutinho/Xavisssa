using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xavissa.Frontend.Migrations
{
    /// <inheritdoc />
    public partial class AddProductOnlineId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OnlineId",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnlineId",
                table: "Products");
        }
    }
}
