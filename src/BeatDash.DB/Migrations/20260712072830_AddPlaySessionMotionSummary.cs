using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddPlaySessionMotionSummary : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "PlaySessionMotionSummaries",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameCount = table.Column<int>(type: "integer", nullable: false),
                    SampleRateHz = table.Column<int>(type: "integer", nullable: false),
                    LeftSaberTravel = table.Column<double>(type: "double precision", nullable: false),
                    RightSaberTravel = table.Column<double>(type: "double precision", nullable: false),
                    HeadTravel = table.Column<double>(type: "double precision", nullable: false),
                    AvgLeftSaberSpeed = table.Column<double>(type: "double precision", nullable: false),
                    AvgRightSaberSpeed = table.Column<double>(type: "double precision", nullable: false),
                    LeftReachRange = table.Column<double>(type: "double precision", nullable: false),
                    RightReachRange = table.Column<double>(type: "double precision", nullable: false),
                    HeadRange = table.Column<double>(type: "double precision", nullable: false),
                    FatigueCurve = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PlaySessionMotionSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaySessionMotionSummaries_PlaySessions_PlaySessionId",
                        column: x => x.PlaySessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionMotionSummaries_PlaySessionId",
                table: "PlaySessionMotionSummaries",
                column: "PlaySessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "PlaySessionMotionSummaries");
        }
    }
}
