using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class NewMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpecialInvoices_Customers_CustomerId",
                table: "SpecialInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SpecialInvoices_CustomerId",
                table: "SpecialInvoices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "SpecialInvoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "SpecialInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_CustomerId",
                table: "SpecialInvoices",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialInvoices_Customers_CustomerId",
                table: "SpecialInvoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }
    }
}
