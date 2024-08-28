using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Input = table.Column<float>(type: "real", nullable: true),
                    Output = table.Column<float>(type: "real", nullable: true),
                    Quantity = table.Column<float>(type: "real", nullable: true),
                    PriceWithTax = table.Column<float>(type: "real", nullable: true),
                    Tax = table.Column<float>(type: "real", nullable: true),
                    Total = table.Column<float>(type: "real", nullable: true),
                    ArticleNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitOfMeasurement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingProducts_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BuildingProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProducts_BuildingId",
                table: "BuildingProducts",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProducts_ProductId",
                table: "BuildingProducts",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingProducts");


            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
