using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHandleAndProfileVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Handle",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileActivityPublic",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileHistoryPublic",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileSkillPublic",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProfileStatsPublic",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Handle",
                table: "Users",
                column: "Handle",
                unique: true,
                filter: "\"Handle\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Handle",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Handle",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileActivityPublic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileHistoryPublic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileSkillPublic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileStatsPublic",
                table: "Users");
        }
    }
}
