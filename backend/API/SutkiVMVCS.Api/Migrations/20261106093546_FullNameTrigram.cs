using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class FullNameTrigram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "FullNameTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullNameTrigram",
                table: "Clients");
        }
    }
}
