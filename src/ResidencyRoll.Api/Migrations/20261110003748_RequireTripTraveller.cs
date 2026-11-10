using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResidencyRoll.Api.Migrations
{
    /// <inheritdoc />
    public partial class RequireTripTraveller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TravellerId",
                table: "Trips",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TravellerId_DepartureDateTime",
                table: "Trips",
                columns: new[] { "TravellerId", "DepartureDateTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Travellers_TravellerId",
                table: "Trips",
                column: "TravellerId",
                principalTable: "Travellers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Travellers_TravellerId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_TravellerId_DepartureDateTime",
                table: "Trips");

            migrationBuilder.AlterColumn<int>(
                name: "TravellerId",
                table: "Trips",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }
    }
}
