using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class RemakeNearby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Distance",
                table: "NearbyPlaces");

            migrationBuilder.AddColumn<string>(
                name: "Distance",
                table: "ApartmentNearby",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Distance",
                table: "ApartmentNearby");

            migrationBuilder.AddColumn<string>(
                name: "Distance",
                table: "NearbyPlaces",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
