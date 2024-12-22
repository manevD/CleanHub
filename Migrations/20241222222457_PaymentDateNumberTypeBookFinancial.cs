using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class PaymentDateNumberTypeBookFinancial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentDate",
                table: "BookFinancials",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentNumber",
                table: "BookFinancials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "BookFinancials",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "BookFinancials");

            migrationBuilder.DropColumn(
                name: "PaymentNumber",
                table: "BookFinancials");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "BookFinancials");
        }
    }
}
