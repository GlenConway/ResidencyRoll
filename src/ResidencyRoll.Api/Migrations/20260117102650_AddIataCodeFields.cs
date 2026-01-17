using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResidencyRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIataCodeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalIataCode",
                table: "Trips",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartureIataCode",
                table: "Trips",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalIataCode",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DepartureIataCode",
                table: "Trips");
        }
    }
}
