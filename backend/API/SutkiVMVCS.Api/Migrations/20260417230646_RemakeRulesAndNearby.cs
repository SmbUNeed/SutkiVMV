using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class RemakeRulesAndNearby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentRules",
                table: "ApartmentRules");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentRules_ApartmentId",
                table: "ApartmentRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentNearby",
                table: "ApartmentNearby");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentNearby_ApartmentId",
                table: "ApartmentNearby");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ApartmentRules");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "ApartmentRules");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "ApartmentRules");

            migrationBuilder.DropColumn(
                name: "Distance",
                table: "ApartmentNearby");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "ApartmentNearby");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ApartmentNearby");

            migrationBuilder.RenameColumn(
                name: "Order",
                table: "ApartmentRules",
                newName: "RuleId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ApartmentNearby",
                newName: "NearbyPlaceId");

            migrationBuilder.AlterColumn<int>(
                name: "NearbyPlaceId",
                table: "ApartmentNearby",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentRules",
                table: "ApartmentRules",
                columns: new[] { "ApartmentId", "RuleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentNearby",
                table: "ApartmentNearby",
                columns: new[] { "ApartmentId", "NearbyPlaceId" });

            migrationBuilder.CreateTable(
                name: "NearbyPlaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Distance = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NearbyPlaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Icon = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentRules_RuleId",
                table: "ApartmentRules",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentNearby_NearbyPlaceId",
                table: "ApartmentNearby",
                column: "NearbyPlaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentNearby_NearbyPlaces_NearbyPlaceId",
                table: "ApartmentNearby",
                column: "NearbyPlaceId",
                principalTable: "NearbyPlaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApartmentRules_Rules_RuleId",
                table: "ApartmentRules",
                column: "RuleId",
                principalTable: "Rules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentNearby_NearbyPlaces_NearbyPlaceId",
                table: "ApartmentNearby");

            migrationBuilder.DropForeignKey(
                name: "FK_ApartmentRules_Rules_RuleId",
                table: "ApartmentRules");

            migrationBuilder.DropTable(
                name: "NearbyPlaces");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentRules",
                table: "ApartmentRules");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentRules_RuleId",
                table: "ApartmentRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApartmentNearby",
                table: "ApartmentNearby");

            migrationBuilder.DropIndex(
                name: "IX_ApartmentNearby_NearbyPlaceId",
                table: "ApartmentNearby");

            migrationBuilder.RenameColumn(
                name: "RuleId",
                table: "ApartmentRules",
                newName: "Order");

            migrationBuilder.RenameColumn(
                name: "NearbyPlaceId",
                table: "ApartmentNearby",
                newName: "Id");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ApartmentRules",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "ApartmentRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "ApartmentRules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "ApartmentNearby",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Distance",
                table: "ApartmentNearby",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "ApartmentNearby",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ApartmentNearby",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentRules",
                table: "ApartmentRules",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApartmentNearby",
                table: "ApartmentNearby",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentRules_ApartmentId",
                table: "ApartmentRules",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentNearby_ApartmentId",
                table: "ApartmentNearby",
                column: "ApartmentId");
        }
    }
}
