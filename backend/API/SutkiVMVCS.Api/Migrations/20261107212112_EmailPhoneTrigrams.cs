using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class EmailPhoneTrigrams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "EmailTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhoneNumberTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailTrigram",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PhoneNumberTrigram",
                table: "Clients");
        }
    }
}
