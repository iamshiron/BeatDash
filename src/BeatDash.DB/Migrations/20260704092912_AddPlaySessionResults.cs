using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddPlaySessionResults : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<float>(
                name: "Results_Accuracy",
                table: "PlaySessions",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_BadCuts",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Results_FinalEnergy",
                table: "PlaySessions",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Results_FullCombo",
                table: "PlaySessions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_GoodCuts",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_MaxCombo",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_MaxPossibleScore",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_Misses",
                table: "PlaySessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Results_Rank",
                table: "PlaySessions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Results_Score",
                table: "PlaySessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Results_Accuracy",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_BadCuts",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_FinalEnergy",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_FullCombo",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_GoodCuts",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_MaxCombo",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_MaxPossibleScore",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_Misses",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_Rank",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "Results_Score",
                table: "PlaySessions");
        }
    }
}
