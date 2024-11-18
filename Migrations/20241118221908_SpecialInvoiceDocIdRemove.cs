using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class SpecialInvoiceDocIdRemove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpecialInvoices_Documents_DocId",
                table: "SpecialInvoices");

            migrationBuilder.DropIndex(
                name: "IX_SpecialInvoices_DocId",
                table: "SpecialInvoices");

            migrationBuilder.DropColumn(
                name: "DocId",
                table: "SpecialInvoices");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ForDate",
                table: "SpecialInvoices",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForDate",
                table: "SpecialInvoices");

            migrationBuilder.AddColumn<int>(
                name: "DocId",
                table: "SpecialInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_DocId",
                table: "SpecialInvoices",
                column: "DocId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialInvoices_Documents_DocId",
                table: "SpecialInvoices",
                column: "DocId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
