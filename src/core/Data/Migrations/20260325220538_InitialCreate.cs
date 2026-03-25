using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Shiron.BeatDash.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SongName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SongSubName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SongAuthor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mapper = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BSRKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    BPM = table.Column<int>(type: "integer", nullable: false),
                    CoverImage = table.Column<string>(type: "text", nullable: true),
                    GameVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SongNameSearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(\"SongName\", ''))", stored: true),
                    SongAuthorSearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(\"SongAuthor\", ''))", stored: true),
                    MapperSearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(\"Mapper\", ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Difficulties",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MapId = table.Column<long>(type: "bigint", nullable: false),
                    MapType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CustomDifficultyLabel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NJS = table.Column<double>(type: "double precision", nullable: false),
                    PP = table.Column<double>(type: "double precision", nullable: false),
                    Star = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Difficulties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Difficulties_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaySessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MapId = table.Column<long>(type: "bigint", nullable: false),
                    DifficultyId = table.Column<long>(type: "bigint", nullable: false),
                    ModifiersMultiplier = table.Column<float>(type: "real", nullable: false),
                    PracticeMode = table.Column<bool>(type: "boolean", nullable: false),
                    PluginVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsMultiplayer = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousRecord = table.Column<int>(type: "integer", nullable: false),
                    PreviousBSR = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FinalScore = table.Column<int>(type: "integer", nullable: false),
                    FinalScoreWithMultipliers = table.Column<int>(type: "integer", nullable: false),
                    FinalMaxScore = table.Column<int>(type: "integer", nullable: false),
                    FinalMaxScoreWithMultipliers = table.Column<int>(type: "integer", nullable: false),
                    FinalRank = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    FinalFullCombo = table.Column<bool>(type: "boolean", nullable: false),
                    FinalCombo = table.Column<int>(type: "integer", nullable: false),
                    FinalMisses = table.Column<int>(type: "integer", nullable: false),
                    FinalAccuracy = table.Column<double>(type: "double precision", nullable: false),
                    FinalTimeElapsed = table.Column<int>(type: "integer", nullable: false),
                    Modifiers = table.Column<string>(type: "jsonb", nullable: false),
                    PracticeModeModifiers = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessions_Difficulties_DifficultyId",
                        column: x => x.DifficultyId,
                        principalTable: "Difficulties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlaySessions_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LiveDataSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlaySessionId = table.Column<long>(type: "bigint", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ScoreWithMultipliers = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    MaxScoreWithMultipliers = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FullCombo = table.Column<bool>(type: "boolean", nullable: false),
                    NotesSpawned = table.Column<int>(type: "integer", nullable: false),
                    Combo = table.Column<int>(type: "integer", nullable: false),
                    Misses = table.Column<int>(type: "integer", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: false),
                    PlayerHealth = table.Column<double>(type: "double precision", nullable: false),
                    TimeElapsed = table.Column<int>(type: "integer", nullable: false),
                    EventTrigger = table.Column<int>(type: "integer", nullable: false),
                    BlockHitPreSwing = table.Column<int>(type: "integer", nullable: false),
                    BlockHitPostSwing = table.Column<int>(type: "integer", nullable: false),
                    BlockHitCenterSwing = table.Column<int>(type: "integer", nullable: false),
                    NoteColorType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveDataSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveDataSnapshots_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConnectionName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    PlaySessionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMessages_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Difficulties_MapId",
                table: "Difficulties",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_Difficulties_MapId_MapType_Difficulty",
                table: "Difficulties",
                columns: new[] { "MapId", "MapType", "Difficulty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LiveDataSnapshots_EventTrigger",
                table: "LiveDataSnapshots",
                column: "EventTrigger");

            migrationBuilder.CreateIndex(
                name: "IX_LiveDataSnapshots_PlaySessionId",
                table: "LiveDataSnapshots",
                column: "PlaySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveDataSnapshots_Timestamp",
                table: "LiveDataSnapshots",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_BSRKey",
                table: "Maps",
                column: "BSRKey");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_Hash",
                table: "Maps",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Maps_Mapper",
                table: "Maps",
                column: "Mapper");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_MapperSearchVector",
                table: "Maps",
                column: "MapperSearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_SongAuthor",
                table: "Maps",
                column: "SongAuthor");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_SongAuthorSearchVector",
                table: "Maps",
                column: "SongAuthorSearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_SongName",
                table: "Maps",
                column: "SongName");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_SongNameSearchVector",
                table: "Maps",
                column: "SongNameSearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_DifficultyId",
                table: "PlaySessions",
                column: "DifficultyId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_FinishedAt",
                table: "PlaySessions",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_MapId",
                table: "PlaySessions",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_StartedAt",
                table: "PlaySessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RawMessages_ConnectionName",
                table: "RawMessages",
                column: "ConnectionName");

            migrationBuilder.CreateIndex(
                name: "IX_RawMessages_PlaySessionId",
                table: "RawMessages",
                column: "PlaySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMessages_Timestamp",
                table: "RawMessages",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveDataSnapshots");

            migrationBuilder.DropTable(
                name: "RawMessages");

            migrationBuilder.DropTable(
                name: "PlaySessions");

            migrationBuilder.DropTable(
                name: "Difficulties");

            migrationBuilder.DropTable(
                name: "Maps");
        }
    }
}
