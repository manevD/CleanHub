using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class Building : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InavtiveFrom",
                table: "Residents",
                newName: "InactiveFrom");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReserveMoney",
                table: "Residents",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NumberOfResidence",
                table: "Buildings",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InactiveFrom",
                table: "Residents",
                newName: "InavtiveFrom");

            migrationBuilder.AlterColumn<int>(
                name: "ReserveMoney",
                table: "Residents",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NumberOfResidence",
                table: "Buildings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
