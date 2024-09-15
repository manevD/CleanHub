using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class BuildingProductAnpassungList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BuildingProducts_ProductId",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "BuildingProducts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "BuildingProducts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProducts_ProductId",
                table: "BuildingProducts",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingProducts_Products_ProductId",
                table: "BuildingProducts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }
    }
}
