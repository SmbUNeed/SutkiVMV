using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SutkiVMVCS.Migrations
{
    /// <inheritdoc />
    public partial class Initialize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Users (создаём первой, т.к. Clients ссылается на Users) ──────────
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email        = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role         = table.Column<string>(type: "text", nullable: false),
                    CreatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // ── Amenities ─────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Amenities",
                columns: table => new
                {
                    Id   = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenities", x => x.Id);
                });

            // ── Apartments ────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Apartments",
                columns: table => new
                {
                    Id            = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title         = table.Column<string>(type: "text", nullable: false),
                    Address       = table.Column<string>(type: "text", nullable: false),
                    Description   = table.Column<string>(type: "text", nullable: false),
                    Guests        = table.Column<int>(type: "integer", nullable: false),
                    PricePerNight = table.Column<decimal>(type: "numeric", nullable: false),
                    RoomsCount    = table.Column<int>(type: "integer", nullable: false),
                    IsActive      = table.Column<bool>(type: "boolean", nullable: false),
                    Area          = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDeleted     = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apartments", x => x.Id);
                });

            // ── Clients (nullable FK → Users) ─────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName    = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    Email       = table.Column<string>(type: "text", nullable: false),
                    UserId      = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ── ApartmentAmenities (join table) ───────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ApartmentAmenities",
                columns: table => new
                {
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    AmenityId  = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentAmenities", x => new { x.ApartmentId, x.AmenityId });
                    table.ForeignKey(
                        name: "FK_ApartmentAmenities_Amenities_AmenityId",
                        column: x => x.AmenityId,
                        principalTable: "Amenities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentAmenities_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── ApartmentGallery ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ApartmentGallery",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    ImagePath   = table.Column<string>(type: "text", nullable: false),
                    IsMain      = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentGallery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentGallery_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Bookings ──────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false),
                    ClientId    = table.Column<int>(type: "integer", nullable: false),
                    StartDate   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalPrice  = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status      = table.Column<string>(type: "text", nullable: false),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── Indexes ───────────────────────────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentAmenities_AmenityId",
                table: "ApartmentAmenities",
                column: "AmenityId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentGallery_ApartmentId",
                table: "ApartmentGallery",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_IsActive_IsDeleted",
                table: "Apartments",
                columns: new[] { "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ApartmentId",
                table: "Bookings",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientId",
                table: "Bookings",
                column: "ClientId");

            // Уникальный индекс: один Client → один User (one-to-one)
            migrationBuilder.CreateIndex(
                name: "IX_Clients_UserId",
                table: "Clients",
                column: "UserId",
                unique: true);

            // Уникальный индекс на email пользователя
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ApartmentAmenities");
            migrationBuilder.DropTable(name: "ApartmentGallery");
            migrationBuilder.DropTable(name: "Bookings");
            migrationBuilder.DropTable(name: "Amenities");
            migrationBuilder.DropTable(name: "Apartments");
            migrationBuilder.DropTable(name: "Clients");
            migrationBuilder.DropTable(name: "Users");
        }
    }
}
