using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class DefaultPublicTokenValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PublicToken",
                table: "Bookings",
                type: "text",
                nullable: false,
                defaultValue: "0bb6052a4b1d253df2c44b4fd6ea7005c986591506a24ac1b03f27df52ad7056",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PublicToken",
                table: "Bookings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "0bb6052a4b1d253df2c44b4fd6ea7005c986591506a24ac1b03f27df52ad7056");
        }
    }
}
