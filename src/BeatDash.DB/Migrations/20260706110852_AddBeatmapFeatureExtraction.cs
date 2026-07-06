using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddBeatmapFeatureExtraction : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<int>(
                name: "FeatureStatus",
                table: "BeatmapDifficultyAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "BeatmapDifficultyAnalyses",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "FeatureStatus",
                table: "BeatmapDifficultyAnalyses");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "BeatmapDifficultyAnalyses");
        }
    }
}
