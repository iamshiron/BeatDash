using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddBeatSaverEntities : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<int>(
                name: "FetchAttemptCount",
                table: "Beatmaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FetchError",
                table: "Beatmaps",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FetchLastAttemptedAt",
                table: "Beatmaps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FetchStatus",
                table: "Beatmaps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BeatSaverUsers",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BeatSaverUserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Avatar = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Admin = table.Column<bool>(type: "boolean", nullable: false),
                    Curator = table.Column<bool>(type: "boolean", nullable: false),
                    SeniorCurator = table.Column<bool>(type: "boolean", nullable: false),
                    PlaylistUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_BeatSaverUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverMaps",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BeatSaverId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Automapper = table.Column<bool>(type: "boolean", nullable: false),
                    Ranked = table.Column<bool>(type: "boolean", nullable: false),
                    Qualified = table.Column<bool>(type: "boolean", nullable: false),
                    BlRanked = table.Column<bool>(type: "boolean", nullable: false),
                    BlQualified = table.Column<bool>(type: "boolean", nullable: false),
                    DeclaredAi = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Uploaded = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BeatSaverCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BeatSaverUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ZipObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata_Bpm = table.Column<float>(type: "real", nullable: false),
                    Metadata_Duration = table.Column<int>(type: "integer", nullable: false),
                    Metadata_SongName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Metadata_SongSubName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Metadata_SongAuthorName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Metadata_LevelAuthorName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Stats_Plays = table.Column<int>(type: "integer", nullable: false),
                    Stats_Downloads = table.Column<int>(type: "integer", nullable: false),
                    Stats_Upvotes = table.Column<int>(type: "integer", nullable: false),
                    Stats_Downvotes = table.Column<int>(type: "integer", nullable: false),
                    Stats_Score = table.Column<float>(type: "real", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeatmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_BeatSaverMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverMaps_BeatSaverUsers_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "BeatSaverUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BeatSaverMaps_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverVersions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SageScore = table.Column<int>(type: "integer", nullable: true),
                    DownloadUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PreviewUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    BeatSaverMapId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_BeatSaverVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverVersions_BeatSaverMaps_BeatSaverMapId",
                        column: x => x.BeatSaverMapId,
                        principalTable: "BeatSaverMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeatSaverVersionDifficulties",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Njs = table.Column<float>(type: "real", nullable: false),
                    Offset = table.Column<float>(type: "real", nullable: false),
                    Notes = table.Column<int>(type: "integer", nullable: false),
                    Bombs = table.Column<int>(type: "integer", nullable: false),
                    Obstacles = table.Column<int>(type: "integer", nullable: false),
                    Nps = table.Column<float>(type: "real", nullable: false),
                    Length = table.Column<float>(type: "real", nullable: false),
                    Characteristic = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Events = table.Column<int>(type: "integer", nullable: false),
                    Chroma = table.Column<bool>(type: "boolean", nullable: false),
                    MappingExtensions = table.Column<bool>(type: "boolean", nullable: false),
                    NoodleExtensions = table.Column<bool>(type: "boolean", nullable: false),
                    Cinema = table.Column<bool>(type: "boolean", nullable: false),
                    Seconds = table.Column<float>(type: "real", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: true),
                    Environment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ParityErrors = table.Column<int>(type: "integer", nullable: false),
                    ParityWarns = table.Column<int>(type: "integer", nullable: false),
                    ParityResets = table.Column<int>(type: "integer", nullable: false),
                    BeatSaverVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_BeatSaverVersionDifficulties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeatSaverVersionDifficulties_BeatSaverVersions_BeatSaverVer~",
                        column: x => x.BeatSaverVersionId,
                        principalTable: "BeatSaverVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_BeatmapId",
                table: "BeatSaverMaps",
                column: "BeatmapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_BeatSaverId",
                table: "BeatSaverMaps",
                column: "BeatSaverId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverMaps_UploaderId",
                table: "BeatSaverMaps",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverUsers_BeatSaverUserId",
                table: "BeatSaverUsers",
                column: "BeatSaverUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersionDifficulties_BeatSaverVersionId",
                table: "BeatSaverVersionDifficulties",
                column: "BeatSaverVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersions_BeatSaverMapId",
                table: "BeatSaverVersions",
                column: "BeatSaverMapId");

            migrationBuilder.CreateIndex(
                name: "IX_BeatSaverVersions_Hash",
                table: "BeatSaverVersions",
                column: "Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "BeatSaverVersionDifficulties");

            migrationBuilder.DropTable(
                name: "BeatSaverVersions");

            migrationBuilder.DropTable(
                name: "BeatSaverMaps");

            migrationBuilder.DropTable(
                name: "BeatSaverUsers");

            migrationBuilder.DropColumn(
                name: "FetchAttemptCount",
                table: "Beatmaps");

            migrationBuilder.DropColumn(
                name: "FetchError",
                table: "Beatmaps");

            migrationBuilder.DropColumn(
                name: "FetchLastAttemptedAt",
                table: "Beatmaps");

            migrationBuilder.DropColumn(
                name: "FetchStatus",
                table: "Beatmaps");
        }
    }
}
