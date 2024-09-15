using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class BuildingProductAnpassung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingProducts_Products_ProductId",
                table: "BuildingProducts");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "BuildingProducts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ArticleNotes",
                table: "BuildingProducts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Input",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Output",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Price",
                table: "BuildingProducts",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "PriceWithTax",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Quantity",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Tax",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Total",
                table: "BuildingProducts",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasurement",
                table: "BuildingProducts",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildingProducts_Products_ProductId",
                table: "BuildingProducts");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingProductViewModel_BuildingViewModel_BuildingViewModelId",
                table: "BuildingProductViewModel");

            migrationBuilder.DropForeignKey(
                name: "FK_BuildingViewModel_DocumentViewModel_DocumentViewModelId",
                table: "BuildingViewModel");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentViewModel_BuildingViewModel_BuildingId",
                table: "DocumentViewModel");

            migrationBuilder.DropIndex(
                name: "IX_DocumentViewModel_BuildingId",
                table: "DocumentViewModel");

            migrationBuilder.DropIndex(
                name: "IX_BuildingViewModel_DocumentViewModelId",
                table: "BuildingViewModel");

            migrationBuilder.DropIndex(
                name: "IX_BuildingProductViewModel_BuildingViewModelId",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "DocumentViewModel");

            migrationBuilder.DropColumn(
                name: "DocumentViewModelId",
                table: "BuildingViewModel");

            migrationBuilder.DropColumn(
                name: "ArticleNotes",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "BuildingViewModelId",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Input",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Output",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "PriceWithTax",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasurement",
                table: "BuildingProductViewModel");

            migrationBuilder.DropColumn(
                name: "ArticleNotes",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Input",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Output",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "PriceWithTax",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "BuildingProducts");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasurement",
                table: "BuildingProducts");

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "BuildingProductViewModel",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "BuildingProducts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ProductViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Input = table.Column<float>(type: "real", nullable: true),
                    Output = table.Column<float>(type: "real", nullable: true),
                    Price = table.Column<float>(type: "real", nullable: false),
                    PriceWithTax = table.Column<float>(type: "real", nullable: true),
                    Quantity = table.Column<float>(type: "real", nullable: true),
                    Tax = table.Column<float>(type: "real", nullable: true),
                    Total = table.Column<float>(type: "real", nullable: true),
                    UnitOfMeasurement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductViewModel", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProductViewModel_BuildingId",
                table: "BuildingProductViewModel",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProductViewModel_ProductId",
                table: "BuildingProductViewModel",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingProducts_Products_ProductId",
                table: "BuildingProducts",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingProductViewModel_BuildingViewModel_BuildingId",
                table: "BuildingProductViewModel",
                column: "BuildingId",
                principalTable: "BuildingViewModel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingProductViewModel_ProductViewModel_ProductId",
                table: "BuildingProductViewModel",
                column: "ProductId",
                principalTable: "ProductViewModel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
