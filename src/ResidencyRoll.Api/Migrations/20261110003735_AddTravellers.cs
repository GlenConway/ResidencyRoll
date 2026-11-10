using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResidencyRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTravellers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Trips",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TravellerId",
                table: "Trips",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "Trips",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Travellers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    HomeCountry = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LinkedUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Travellers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Travellers_LinkedUserId",
                table: "Travellers",
                column: "LinkedUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Travellers_OwnerUserId",
                table: "Travellers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Travellers_OwnerUserId_Name",
                table: "Travellers",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Travellers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TravellerId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Trips");
        }
    }
}
