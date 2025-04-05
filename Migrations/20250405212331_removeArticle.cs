using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class removeArticle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Books_Articles_ArticleId",
                table: "Books");

            migrationBuilder.DropTable(
                name: "BookFinancialSub");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Articles",
                table: "Articles");

            migrationBuilder.RenameTable(
                name: "Articles",
                newName: "Article");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Article",
                table: "Article",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_Article_ArticleId",
                table: "Books",
                column: "ArticleId",
                principalTable: "Article",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Books_Article_ArticleId",
                table: "Books");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Article",
                table: "Article");

            migrationBuilder.RenameTable(
                name: "Article",
                newName: "Articles");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Articles",
                table: "Articles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "BookFinancialSub",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookFinancialId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Demands = table.Column<float>(type: "real", nullable: true),
                    Owes = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookFinancialSub", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookFinancialSub_BookFinancials_BookFinancialId",
                        column: x => x.BookFinancialId,
                        principalTable: "BookFinancials",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancialSub_BookFinancialId",
                table: "BookFinancialSub",
                column: "BookFinancialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_Articles_ArticleId",
                table: "Books",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id");
        }
    }
}
