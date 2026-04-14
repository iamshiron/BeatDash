using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.API.Migrations {
    /// <inheritdoc />
    public partial class AddPlaySessionCompletedAndFullCleared : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "PlaySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FullCleared",
                table: "PlaySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_Completed",
                table: "PlaySessions",
                column: "Completed");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_FullCleared",
                table: "PlaySessions",
                column: "FullCleared");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_Completed",
                table: "PlaySessions");

            migrationBuilder.DropIndex(
                name: "IX_PlaySessions_FullCleared",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "FullCleared",
                table: "PlaySessions");
        }
    }
}
