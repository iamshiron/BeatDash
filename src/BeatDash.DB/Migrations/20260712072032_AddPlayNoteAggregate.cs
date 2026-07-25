using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddPlayNoteAggregate : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<DateTime>(
                name: "AggregatedAt",
                table: "PlaySessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayNoteAggregates",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacteristicSerializedName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ColorType = table.Column<int>(type: "integer", nullable: false),
                    CutDirection = table.Column<int>(type: "integer", nullable: false),
                    LineIndex = table.Column<int>(type: "integer", nullable: false),
                    NoteLineLayer = table.Column<int>(type: "integer", nullable: false),
                    NoteCount = table.Column<long>(type: "bigint", nullable: false),
                    GoodCount = table.Column<long>(type: "bigint", nullable: false),
                    MissCount = table.Column<long>(type: "bigint", nullable: false),
                    BadCount = table.Column<long>(type: "bigint", nullable: false),
                    SumEarnedScore = table.Column<long>(type: "bigint", nullable: false),
                    SumMaxScore = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlayNoteAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayNoteAggregates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayNoteAggregates_UserId_CharacteristicSerializedName_Colo~",
                table: "PlayNoteAggregates",
                columns: new[] { "UserId", "CharacteristicSerializedName", "ColorType", "CutDirection", "LineIndex", "NoteLineLayer" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "PlayNoteAggregates");

            migrationBuilder.DropColumn(
                name: "AggregatedAt",
                table: "PlaySessions");
        }
    }
}
