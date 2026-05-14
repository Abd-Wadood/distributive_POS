using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class LanTerminalConcurrencySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "UserSessions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "MAIN-01");

            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "UserSessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "Purchases",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "MAIN-01");

            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "Purchases",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "Orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "MAIN-01");

            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "InventoryTransactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "MAIN-01");

            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "InventoryTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Terminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerminalCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Terminals_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerminalHeartbeats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerminalId = table.Column<int>(type: "integer", nullable: false),
                    TerminalCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentUserId = table.Column<string>(type: "text", nullable: true),
                    CurrentSessionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminalHeartbeats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TerminalHeartbeats_AspNetUsers_CurrentUserId",
                        column: x => x.CurrentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminalHeartbeats_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminalHeartbeats_Terminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TerminalHeartbeats_UserSessions_CurrentSessionId",
                        column: x => x.CurrentSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Terminals",
                columns: new[] { "Id", "BranchId", "CreatedAt", "IpAddress", "IsActive", "Name", "TerminalCode", "UpdatedAt" },
                values: new object[] { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Main Terminal", "MAIN-01", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_TerminalId",
                table: "UserSessions",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TerminalId",
                table: "Purchases",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TerminalId",
                table: "Orders",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TerminalId",
                table: "InventoryTransactions",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalHeartbeats_BranchId",
                table: "TerminalHeartbeats",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalHeartbeats_CurrentSessionId",
                table: "TerminalHeartbeats",
                column: "CurrentSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalHeartbeats_CurrentUserId",
                table: "TerminalHeartbeats",
                column: "CurrentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalHeartbeats_TerminalId",
                table: "TerminalHeartbeats",
                column: "TerminalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_BranchId",
                table: "Terminals",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_TerminalCode",
                table: "Terminals",
                column: "TerminalCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_Terminals_TerminalId",
                table: "InventoryTransactions",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Terminals_TerminalId",
                table: "Orders",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Terminals_TerminalId",
                table: "Purchases",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Terminals_TerminalId",
                table: "UserSessions",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_Terminals_TerminalId",
                table: "InventoryTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Terminals_TerminalId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Terminals_TerminalId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Terminals_TerminalId",
                table: "UserSessions");

            migrationBuilder.DropTable(
                name: "TerminalHeartbeats");

            migrationBuilder.DropTable(
                name: "Terminals");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_TerminalId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TerminalId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TerminalId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_TerminalId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "InventoryTransactions");
        }
    }
}
