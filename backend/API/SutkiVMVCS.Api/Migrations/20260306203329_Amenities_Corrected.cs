using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class Amenities_Corrected : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentAmenities_Amenities_AmenitieId",
                table: "ApartmentAmenities");

            migrationBuilder.RenameColumn(
                name: "AmenitieId",
                table: "ApartmentAmenities",
                newName: "AmenityId");

            migrationBuilder.RenameIndex(
                name: "IX_ApartmentAmenities_AmenitieId",
                table: "ApartmentAmenities",
                newName: "IX_ApartmentAmenities_AmenityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentAmenities_Amenities_AmenityId",
                table: "ApartmentAmenities",
                column: "AmenityId",
                principalTable: "Amenities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentAmenities_Amenities_AmenityId",
                table: "ApartmentAmenities");

            migrationBuilder.RenameColumn(
                name: "AmenityId",
                table: "ApartmentAmenities",
                newName: "AmenitieId");

            migrationBuilder.RenameIndex(
                name: "IX_ApartmentAmenities_AmenityId",
                table: "ApartmentAmenities",
                newName: "IX_ApartmentAmenities_AmenitieId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentAmenities_Amenities_AmenitieId",
                table: "ApartmentAmenities",
                column: "AmenitieId",
                principalTable: "Amenities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
