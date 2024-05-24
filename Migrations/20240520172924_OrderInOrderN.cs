using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class OrderInOrderN : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Оrder",
                table: "BookFinancials");

            migrationBuilder.AddColumn<int>(
                name: "OrderN",
                table: "BookFinancials",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderN",
                table: "BookFinancials");

            migrationBuilder.AddColumn<int>(
                name: "Оrder",
                table: "BookFinancials",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
