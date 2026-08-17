using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanHub.Migrations
{
    /// <inheritdoc />
    public partial class ProduktiCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Saldo",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Saldo1201",
                table: "Customers");

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajAdministrativniTrosoci",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajCistenjeVlez",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajKomunalnaTaksaJavnoOsvetluvanje",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajOdrzuvanjeLift",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajOdrzuvanjeSmetki",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajPotrosenaElektricnaEnergija",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajRezervenFond",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PresmetajUpravitel",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresmetajAdministrativniTrosoci",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajCistenjeVlez",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajKomunalnaTaksaJavnoOsvetluvanje",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajOdrzuvanjeLift",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajOdrzuvanjeSmetki",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajPotrosenaElektricnaEnergija",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajRezervenFond",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PresmetajUpravitel",
                table: "Customers");

            migrationBuilder.AddColumn<int>(
                name: "Saldo",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Saldo1201",
                table: "Customers",
                type: "int",
                nullable: true);
        }
    }
}
