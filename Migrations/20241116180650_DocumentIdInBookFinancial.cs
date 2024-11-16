using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class DocumentIdInBookFinancial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentId",
                table: "BookFinancials",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancials_DocumentId",
                table: "BookFinancials",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookFinancials_Documents_DocumentId",
                table: "BookFinancials",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookFinancials_Documents_DocumentId",
                table: "BookFinancials");

            migrationBuilder.DropForeignKey(
                name: "FK_BookFinancialViewModel_DocumentViewModel_DocumentId",
                table: "BookFinancialViewModel");

            migrationBuilder.DropIndex(
                name: "IX_BookFinancialViewModel_DocumentId",
                table: "BookFinancialViewModel");

            migrationBuilder.DropIndex(
                name: "IX_BookFinancials_DocumentId",
                table: "BookFinancials");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "BookFinancialViewModel");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "BookFinancials");
        }
    }
}
