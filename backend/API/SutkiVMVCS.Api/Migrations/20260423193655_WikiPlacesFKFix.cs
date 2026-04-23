using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class WikiPlacesFKFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NearbyPlaces_WikiPlace_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropIndex(
                name: "IX_NearbyPlaces_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WikiPlace",
                table: "WikiPlace");

            migrationBuilder.DropColumn(
                name: "WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.RenameTable(
                name: "WikiPlace",
                newName: "WikiPlaces");

            migrationBuilder.AddColumn<int>(
                name: "NearbyPlaceId",
                table: "WikiPlaces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WikiPlaces",
                table: "WikiPlaces",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPlaces_NearbyPlaceId",
                table: "WikiPlaces",
                column: "NearbyPlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WikiPlaces_NearbyPlaces_NearbyPlaceId",
                table: "WikiPlaces",
                column: "NearbyPlaceId",
                principalTable: "NearbyPlaces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WikiPlaces_NearbyPlaces_NearbyPlaceId",
                table: "WikiPlaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_WikiPlaces",
                table: "WikiPlaces");

            migrationBuilder.DropIndex(
                name: "IX_WikiPlaces_NearbyPlaceId",
                table: "WikiPlaces");

            migrationBuilder.DropColumn(
                name: "NearbyPlaceId",
                table: "WikiPlaces");

            migrationBuilder.RenameTable(
                name: "WikiPlaces",
                newName: "WikiPlace");

            migrationBuilder.AddColumn<int>(
                name: "WikiPlaceId",
                table: "NearbyPlaces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WikiPlace",
                table: "WikiPlace",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_NearbyPlaces_WikiPlaceId",
                table: "NearbyPlaces",
                column: "WikiPlaceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NearbyPlaces_WikiPlace_WikiPlaceId",
                table: "NearbyPlaces",
                column: "WikiPlaceId",
                principalTable: "WikiPlace",
                principalColumn: "Id");
        }
    }
}
