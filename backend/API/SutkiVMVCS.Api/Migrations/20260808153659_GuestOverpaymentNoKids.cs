using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class GuestOverpaymentNoKids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kids",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "KidOverpayment",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Kids",
                table: "Apartments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kids",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "KidOverpayment",
                table: "Apartments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Kids",
                table: "Apartments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
