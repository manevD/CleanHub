using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddressBuilding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_Addresses_AddressId",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_AddressId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "NumberOfUnits",
                table: "Buildings",
                newName: "NumberOfResidence");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Buildings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Buildings");

            migrationBuilder.RenameColumn(
                name: "NumberOfResidence",
                table: "Buildings",
                newName: "NumberOfUnits");

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Buildings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_AddressId",
                table: "Buildings",
                column: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_Addresses_AddressId",
                table: "Buildings",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
