using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class FixMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_PublicToken",
                table: "Bookings");

            migrationBuilder.AlterColumn<string>(
                name: "PublicToken",
                table: "Bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PublicToken",
                table: "Bookings",
                column: "PublicToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_PublicToken",
                table: "Bookings");

            migrationBuilder.AlterColumn<string>(
                name: "PublicToken",
                table: "Bookings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PublicToken",
                table: "Bookings",
                column: "PublicToken",
                unique: true);
        }
    }
}
