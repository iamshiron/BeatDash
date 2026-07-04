using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddPlaySessionEntities : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "PlaySessions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeatmapDifficultyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessions_BeatmapDifficulties_BeatmapDifficultyId",
                        column: x => x.BeatmapDifficultyId,
                        principalTable: "BeatmapDifficulties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaySessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionComboBreakItems",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ComboBefore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionComboBreakItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionComboBreakItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionEnergyChangeItems",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTimeMs = table.Column<int>(type: "integer", nullable: false),
                    EnergyBefore = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionEnergyChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionEnergyChangeItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionNoteItems",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ColorType = table.Column<int>(type: "integer", nullable: false),
                    NoteType = table.Column<int>(type: "integer", nullable: false),
                    CutDirection = table.Column<int>(type: "integer", nullable: false),
                    LineIndex = table.Column<int>(type: "integer", nullable: false),
                    NoteLineLayer = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    PreCutSwing = table.Column<float>(type: "real", nullable: false),
                    PostCutSwing = table.Column<float>(type: "real", nullable: false),
                    CutPointDistance = table.Column<float>(type: "real", nullable: false),
                    SaberSpeed = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionNoteItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionScoreChangeItems",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTimeMs = table.Column<int>(type: "integer", nullable: false),
                    ScoreBefore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionScoreChangeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionScoreChangeItems_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionComboBreakItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionComboBreakItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionEnergyChangeItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionEnergyChangeItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionNoteItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionNoteItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_BeatmapDifficultyId",
                table: "PlaySessions",
                column: "BeatmapDifficultyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_UserId",
                table: "PlaySessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionScoreChangeItems_PlaySessionId_SongTimeMs",
                table: "PlaySessionScoreChangeItems",
                columns: new[] { "PlaySessionId", "SongTimeMs" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "PlaySessionComboBreakItems");

            migrationBuilder.DropTable(
                name: "PlaySessionEnergyChangeItems");

            migrationBuilder.DropTable(
                name: "PlaySessionNoteItems");

            migrationBuilder.DropTable(
                name: "PlaySessionScoreChangeItems");

            migrationBuilder.DropTable(
                name: "PlaySessions");
        }
    }
}
