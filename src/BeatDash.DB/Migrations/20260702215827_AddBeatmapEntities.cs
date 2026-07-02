using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddBeatmapEntities : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "Beatmaps",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SongName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SongSubName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SongAuthor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mapper = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Bpm = table.Column<float>(type: "real", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    CoverImageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Beatmaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beatmaps_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BeatmapDifficulties",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DifficultyRank = table.Column<int>(type: "integer", nullable: false),
                    DifficultyName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NotesPerSecond = table.Column<float>(type: "real", nullable: false),
                    CuttableObjectCount = table.Column<int>(type: "integer", nullable: false),
                    BombCount = table.Column<int>(type: "integer", nullable: false),
                    ObstacleCount = table.Column<int>(type: "integer", nullable: false),
                    LaneCount = table.Column<int>(type: "integer", nullable: false),
                    NoteJumpSpeed = table.Column<float>(type: "real", nullable: true),
                    CharacteristicSerializedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CharacteristicColorCount = table.Column<int>(type: "integer", nullable: false),
                    CharacteristicRequires360Movement = table.Column<bool>(type: "boolean", nullable: false),
                    CharacteristicContainsRotationEvents = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeatmapId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_BeatmapDifficulties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatmapDifficulties_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BeatmapDifficulties_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeatmapDifficulties_BeatmapId_CharacteristicSerializedName_~",
                table: "BeatmapDifficulties",
                columns: new[] { "BeatmapId", "CharacteristicSerializedName", "DifficultyRank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatmapDifficulties_SubmittedByUserId",
                table: "BeatmapDifficulties",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Beatmaps_LevelId",
                table: "Beatmaps",
                column: "LevelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beatmaps_SubmittedByUserId",
                table: "Beatmaps",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "BeatmapDifficulties");

            migrationBuilder.DropTable(
                name: "Beatmaps");
        }
    }
}
