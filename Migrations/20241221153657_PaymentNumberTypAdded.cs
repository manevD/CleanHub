using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class PaymentNumberTypAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentDate",
                table: "Documents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentNumber",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PaymentNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Documents");
        }
    }
}
