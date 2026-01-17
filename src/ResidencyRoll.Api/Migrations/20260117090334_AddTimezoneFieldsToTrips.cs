using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResidencyRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTimezoneFieldsToTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns
            migrationBuilder.AddColumn<string>(
                name: "ArrivalCity",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ArrivalCountry",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalDateTime",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTimezone",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.AddColumn<string>(
                name: "DepartureCity",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartureCountry",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureDateTime",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DepartureTimezone",
                table: "Trips",
                type: "TEXT",
                nullable: false,
                defaultValue: "UTC");

            // Migrate existing data from old columns to new columns
            // Map: CountryName -> ArrivalCountry, StartDate -> ArrivalDateTime, EndDate -> DepartureDateTime
            migrationBuilder.Sql(@"
                UPDATE Trips 
                SET 
                    ArrivalCountry = CountryName,
                    ArrivalDateTime = StartDate,
                    DepartureDateTime = EndDate,
                    DepartureCountry = '',
                    ArrivalCity = '',
                    DepartureCity = '',
                    ArrivalTimezone = 'UTC',
                    DepartureTimezone = 'UTC'
                WHERE ArrivalCountry = '' OR ArrivalDateTime = '0001-01-01 00:00:00';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalCity",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalCountry",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalDateTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalTimezone",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DepartureCity",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DepartureCountry",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DepartureDateTime",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DepartureTimezone",
                table: "Trips");
        }
    }
}
