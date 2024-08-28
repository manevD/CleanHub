using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingProductsPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Price",
                table: "Products",
                type: "real",
                nullable: false,
                defaultValue: 0f);
     
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Products");
        }
    }
}
