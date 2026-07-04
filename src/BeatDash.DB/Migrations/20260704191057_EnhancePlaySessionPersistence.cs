using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class EnhancePlaySessionPersistence : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.RenameColumn(
                name: "ScoreBefore",
                table: "PlaySessionScoreChangeItems",
                newName: "Score");

            migrationBuilder.RenameColumn(
                name: "EnergyBefore",
                table: "PlaySessionEnergyChangeItems",
                newName: "Energy");

            migrationBuilder.AddColumn<int>(
                name: "EndReason",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifierFlags",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_EndSongTimeMs",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_MultipliedScore",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AfterCutScore",
                table: "PlaySessionNoteItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BeforeCutScore",
                table: "PlaySessionNoteItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CenterDistanceScore",
                table: "PlaySessionNoteItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScoringType",
                table: "PlaySessionNoteItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "ModifierFlags",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_EndSongTimeMs",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_MultipliedScore",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "AfterCutScore",
                table: "PlaySessionNoteItems");

            migrationBuilder.DropColumn(
                name: "BeforeCutScore",
                table: "PlaySessionNoteItems");

            migrationBuilder.DropColumn(
                name: "CenterDistanceScore",
                table: "PlaySessionNoteItems");

            migrationBuilder.DropColumn(
                name: "ScoringType",
                table: "PlaySessionNoteItems");

            migrationBuilder.RenameColumn(
                name: "Score",
                table: "PlaySessionScoreChangeItems",
                newName: "ScoreBefore");

            migrationBuilder.RenameColumn(
                name: "Energy",
                table: "PlaySessionEnergyChangeItems",
                newName: "EnergyBefore");
        }
    }
}
