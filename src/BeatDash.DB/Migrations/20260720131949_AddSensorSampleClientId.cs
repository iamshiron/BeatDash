using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shiron.BeatDash.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorSampleClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "SensorSamples",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorSamples_ClientId",
                table: "SensorSamples",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_SensorSamples_HealthProxyClients_ClientId",
                table: "SensorSamples",
                column: "ClientId",
                principalTable: "HealthProxyClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SensorSamples_HealthProxyClients_ClientId",
                table: "SensorSamples");

            migrationBuilder.DropIndex(
                name: "IX_SensorSamples_ClientId",
                table: "SensorSamples");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "SensorSamples");
        }
    }
}
