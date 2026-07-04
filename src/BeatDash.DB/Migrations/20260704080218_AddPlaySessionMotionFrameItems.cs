using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddPlaySessionMotionFrameItems : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "PlaySessionItemMotionFrames",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<int>(type: "integer", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongTimeMs = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    FrameCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionItemMotionFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionItemMotionFrames_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionItemMotionFrames_PlaySessionId_SongTimeMs",
                table: "PlaySessionItemMotionFrames",
                columns: new[] { "PlaySessionId", "SongTimeMs" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "PlaySessionItemMotionFrames");
        }
    }
}
