using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class SpecialInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookFinancials_Invoice_SmetkaId",
                table: "BookFinancials");

            migrationBuilder.RenameColumn(
                name: "SmetkaId",
                table: "BookFinancials",
                newName: "InvoiceId");

            migrationBuilder.RenameIndex(
                name: "IX_BookFinancials_SmetkaId",
                table: "BookFinancials",
                newName: "IX_BookFinancials_InvoiceId");

            migrationBuilder.CreateTable(
                name: "SpecialInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialInvoices_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SpecialInvoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SpecialInvoices_Documents_DocId",
                        column: x => x.DocId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecialInvoices_Invoice_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_BuildingId",
                table: "SpecialInvoices",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_CustomerId",
                table: "SpecialInvoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_DocId",
                table: "SpecialInvoices",
                column: "DocId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialInvoices_InvoiceId",
                table: "SpecialInvoices",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookFinancials_Invoice_InvoiceId",
                table: "BookFinancials",
                column: "InvoiceId",
                principalTable: "Invoice",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookFinancials_Invoice_InvoiceId",
                table: "BookFinancials");

            migrationBuilder.DropTable(
                name: "SpecialInvoices");

            migrationBuilder.RenameColumn(
                name: "InvoiceId",
                table: "BookFinancials",
                newName: "SmetkaId");

            migrationBuilder.RenameIndex(
                name: "IX_BookFinancials_InvoiceId",
                table: "BookFinancials",
                newName: "IX_BookFinancials_SmetkaId");

            migrationBuilder.CreateTable(
                name: "ActivityViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityViewModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurschaceCalculation = table.Column<bool>(type: "bit", nullable: true),
                    ShortDescription = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleViewModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookFinancialViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    DocumentTypId = table.Column<int>(type: "int", nullable: true),
                    SmetkaId = table.Column<int>(type: "int", nullable: true),
                    DateTimeChanges = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatumF = table.Column<DateOnly>(type: "date", nullable: true),
                    Demands = table.Column<double>(type: "float", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderN = table.Column<int>(type: "int", nullable: true),
                    Owes = table.Column<double>(type: "float", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookFinancialViewModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookFinancialViewModel_DocumentTyp_DocumentTypId",
                        column: x => x.DocumentTypId,
                        principalTable: "DocumentTyp",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookFinancialViewModel_Invoice_SmetkaId",
                        column: x => x.SmetkaId,
                        principalTable: "Invoice",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BookViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: true),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ArticleNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocId = table.Column<int>(type: "int", nullable: false),
                    Input = table.Column<float>(type: "real", nullable: true),
                    Output = table.Column<float>(type: "real", nullable: true),
                    PriceWithTax = table.Column<float>(type: "real", nullable: true),
                    Quantity = table.Column<float>(type: "real", nullable: true),
                    Tax = table.Column<float>(type: "real", nullable: true),
                    Total = table.Column<float>(type: "real", nullable: true),
                    UnitOfMeasurement = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookViewModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookViewModel_ArticleViewModel_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "ArticleViewModel",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BuildingProductViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    BuildingViewModelId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_BuildingProductViewModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentViewModelId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReserveFund = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingViewModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    Adress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Inactive = table.Column<bool>(type: "bit", nullable: true),
                    InactiveDatum = table.Column<DateOnly>(type: "date", nullable: true),
                    PartnerOpis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicalPerson = table.Column<bool>(type: "bit", nullable: true),
                    Web = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerViewModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerViewModel_ActivityViewModel_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "ActivityViewModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerViewModel_BuildingViewModel_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "BuildingViewModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentViewModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    DateReceived = table.Column<DateOnly>(type: "date", nullable: true),
                    DateTimeChanged = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsForPdf = table.Column<bool>(type: "bit", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: true),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    ToDocument = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalInput = table.Column<float>(type: "real", nullable: true),
                    TotalOutput = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentViewModel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentViewModel_BuildingViewModel_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "BuildingViewModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentViewModel_CustomerViewModel_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerViewModel",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancialViewModel_CustomerId",
                table: "BookFinancialViewModel",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancialViewModel_DocumentId",
                table: "BookFinancialViewModel",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancialViewModel_DocumentTypId",
                table: "BookFinancialViewModel",
                column: "DocumentTypId");

            migrationBuilder.CreateIndex(
                name: "IX_BookFinancialViewModel_SmetkaId",
                table: "BookFinancialViewModel",
                column: "SmetkaId");

            migrationBuilder.CreateIndex(
                name: "IX_BookViewModel_ArticleId",
                table: "BookViewModel",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_BookViewModel_DocumentId",
                table: "BookViewModel",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingProductViewModel_BuildingViewModelId",
                table: "BuildingProductViewModel",
                column: "BuildingViewModelId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingViewModel_DocumentViewModelId",
                table: "BuildingViewModel",
                column: "DocumentViewModelId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerViewModel_ActivityId",
                table: "CustomerViewModel",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerViewModel_BuildingId",
                table: "CustomerViewModel",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentViewModel_BuildingId",
                table: "DocumentViewModel",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentViewModel_CustomerId",
                table: "DocumentViewModel",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookFinancials_Invoice_SmetkaId",
                table: "BookFinancials",
                column: "SmetkaId",
                principalTable: "Invoice",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookFinancialViewModel_CustomerViewModel_CustomerId",
                table: "BookFinancialViewModel",
                column: "CustomerId",
                principalTable: "CustomerViewModel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookFinancialViewModel_DocumentViewModel_DocumentId",
                table: "BookFinancialViewModel",
                column: "DocumentId",
                principalTable: "DocumentViewModel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BookViewModel_DocumentViewModel_DocumentId",
                table: "BookViewModel",
                column: "DocumentId",
                principalTable: "DocumentViewModel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingProductViewModel_BuildingViewModel_BuildingViewModelId",
                table: "BuildingProductViewModel",
                column: "BuildingViewModelId",
                principalTable: "BuildingViewModel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildingViewModel_DocumentViewModel_DocumentViewModelId",
                table: "BuildingViewModel",
                column: "DocumentViewModelId",
                principalTable: "DocumentViewModel",
                principalColumn: "Id");
        }
    }
}
