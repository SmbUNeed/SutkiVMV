using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class ClientForExternalBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "ExternalBookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalBookings_ClientId",
                table: "ExternalBookings",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalBookings_Clients_ClientId",
                table: "ExternalBookings",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalBookings_Clients_ClientId",
                table: "ExternalBookings");

            migrationBuilder.DropIndex(
                name: "IX_ExternalBookings_ClientId",
                table: "ExternalBookings");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "ExternalBookings");
        }
    }
}
