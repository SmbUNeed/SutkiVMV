using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class GuestsAndHoursOverpayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KidOverprice",
                table: "Apartments",
                newName: "KidOverpayment");

            migrationBuilder.RenameColumn(
                name: "HourOverPrice",
                table: "Apartments",
                newName: "HourOverpayment");

            migrationBuilder.RenameColumn(
                name: "GuestOverprice",
                table: "Apartments",
                newName: "GuestOverpayment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KidOverpayment",
                table: "Apartments",
                newName: "KidOverprice");

            migrationBuilder.RenameColumn(
                name: "HourOverpayment",
                table: "Apartments",
                newName: "HourOverPrice");

            migrationBuilder.RenameColumn(
                name: "GuestOverpayment",
                table: "Apartments",
                newName: "GuestOverprice");
        }
    }
}
