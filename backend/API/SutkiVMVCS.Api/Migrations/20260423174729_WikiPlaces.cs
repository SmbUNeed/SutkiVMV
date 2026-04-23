using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class WikiPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WikiPlaceId",
                table: "NearbyPlaces",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WikiPlace",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PhotoPath = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPlace", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NearbyPlaces_WikiPlace_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropTable(
                name: "WikiPlace");

            migrationBuilder.DropIndex(
                name: "IX_NearbyPlaces_WikiPlaceId",
                table: "NearbyPlaces");

            migrationBuilder.DropColumn(
                name: "WikiPlaceId",
                table: "NearbyPlaces");
        }
    }
}
