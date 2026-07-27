using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class GuestsAndHoursOverprice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Guests",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kids",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "GuestOverprice",
                table: "Apartments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HourOverPrice",
                table: "Apartments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "KidOverprice",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Guests",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Kids",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GuestOverprice",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "HourOverPrice",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "KidOverprice",
                table: "Apartments");

            migrationBuilder.DropColumn(
                name: "Kids",
                table: "Apartments");
        }
    }
}
