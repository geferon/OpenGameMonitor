using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OpenGameMonitorDBMigrations.Migrations
{
    public partial class ResourceTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerResourceMonitoring",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ServerId = table.Column<int>(nullable: true),
                    TakenAt = table.Column<DateTime>(nullable: false),
                    CPUUsage = table.Column<double>(nullable: false),
                    MemoryUsage = table.Column<long>(nullable: false),
                    ActivePlayers = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerResourceMonitoring", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServerResourceMonitoring_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerResourceMonitoring_ServerId",
                table: "ServerResourceMonitoring",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerResourceMonitoring");
        }
    }
}
