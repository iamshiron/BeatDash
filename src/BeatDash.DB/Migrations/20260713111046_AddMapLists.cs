using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations {
    /// <inheritdoc />
    public partial class AddMapLists : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "MapLists",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_MapLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapListItems",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MapListId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeatmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_MapListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapListItems_Beatmaps_BeatmapId",
                        column: x => x.BeatmapId,
                        principalTable: "Beatmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapListItems_MapLists_MapListId",
                        column: x => x.MapListId,
                        principalTable: "MapLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapListItems_BeatmapId",
                table: "MapListItems",
                column: "BeatmapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapListItems_MapListId_BeatmapId",
                table: "MapListItems",
                columns: new[] { "MapListId", "BeatmapId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapLists_UserId",
                table: "MapLists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "MapListItems");

            migrationBuilder.DropTable(
                name: "MapLists");
        }
    }
}
