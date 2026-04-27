using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiPlaceToNearbyPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WikiPlaces_NearbyPlaceId",
                table: "WikiPlaces",
                column: "NearbyPlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_WikiPlaces_NearbyPlaces_NearbyPlaceId",
                table: "WikiPlaces",
                column: "NearbyPlaceId",
                principalTable: "NearbyPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddColumn<int>(
                name: "WikiPlaceId",
                table: "NearbyPlaces",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NearbyPlaces_WikiPlaceId",
                table: "NearbyPlaces",
                column: "WikiPlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_NearbyPlaces_WikiPlaces_WikiPlaceId",
                table: "NearbyPlaces",
                column: "WikiPlaceId",
                principalTable: "WikiPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WikiPlaces_NearbyPlaces_NearbyPlaceId",
                table: "WikiPlaces");

            migrationBuilder.DropIndex(
                name: "IX_WikiPlaces_NearbyPlaceId",
                table: "WikiPlaces");

            migrationBuilder.AddColumn<int>(
                name: "NearbyPlaceId1",
                table: "WikiPlaces",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WikiPlaces_NearbyPlaceId1",
                table: "WikiPlaces",
                column: "NearbyPlaceId1");

            migrationBuilder.AddForeignKey(
                name: "FK_WikiPlaces_NearbyPlaces_NearbyPlaceId1",
                table: "WikiPlaces",
                column: "NearbyPlaceId1",
                principalTable: "NearbyPlaces",
                principalColumn: "Id");
            migrationBuilder.DropForeignKey(
                name: "FK_NearbyPlaces_WikiPlaces_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropIndex(
                name: "IX_NearbyPlaces_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropColumn(
                name: "WikiPlaceId",
                table: "NearbyPlaces");
        }
    }
}
