using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddHealthMetadata : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BodyFatPercent",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HealthTrackingEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HeightCm",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestingHeartRate",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WeightKg",
                table: "Users",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BodyFatPercent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HealthTrackingEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RestingHeartRate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "Users");
        }
    }
}
