using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class Trigrams_UserClientRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_UserId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Clients_UserId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EmailTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "NameTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhoneTrigram",
                table: "Clients",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClientId",
                table: "Users",
                column: "ClientId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Clients_ClientId",
                table: "Users",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Clients_ClientId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ClientId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailTrigram",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "NameTrigram",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PhoneTrigram",
                table: "Clients");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UserId",
                table: "Clients",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_UserId",
                table: "Clients",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
