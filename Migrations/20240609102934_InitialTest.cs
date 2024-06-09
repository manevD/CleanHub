using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class InitialTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Buildings_BuidlingId",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "BuidlingId",
                table: "Customers",
                newName: "BuildingId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_BuidlingId",
                table: "Customers",
                newName: "IX_Customers_BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Buildings_BuildingId",
                table: "Customers",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Buildings_BuildingId",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "BuildingId",
                table: "Customers",
                newName: "BuidlingId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_BuildingId",
                table: "Customers",
                newName: "IX_Customers_BuidlingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Buildings_BuidlingId",
                table: "Customers",
                column: "BuidlingId",
                principalTable: "Buildings",
                principalColumn: "Id");
        }
    }
}
