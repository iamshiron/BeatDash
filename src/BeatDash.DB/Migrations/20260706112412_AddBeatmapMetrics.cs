using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddBeatmapMetrics : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "Characteristics",
                table: "BeatmapDifficultyAnalyses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DifficultyRating",
                table: "BeatmapDifficultyAnalyses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricStatus",
                table: "BeatmapDifficultyAnalyses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Pp",
                table: "BeatmapDifficultyAnalyses",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Characteristics",
                table: "BeatmapDifficultyAnalyses");

            migrationBuilder.DropColumn(
                name: "DifficultyRating",
                table: "BeatmapDifficultyAnalyses");

            migrationBuilder.DropColumn(
                name: "MetricStatus",
                table: "BeatmapDifficultyAnalyses");

            migrationBuilder.DropColumn(
                name: "Pp",
                table: "BeatmapDifficultyAnalyses");
        }
    }
}
