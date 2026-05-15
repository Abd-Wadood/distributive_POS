using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class PosHardeningAuditTerminalSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                newName: "UX_UserSessions_UserId_Active");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_SessionCode",
                table: "UserSessions",
                newName: "UX_UserSessions_SessionCode");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_PublicId",
                table: "UserSessions",
                newName: "UX_UserSessions_PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessionHeartbeats_UserSessionId",
                table: "UserSessionHeartbeats",
                newName: "UX_UserSessionHeartbeats_UserSessionId");

            migrationBuilder.RenameIndex(
                name: "IX_Terminals_TerminalCode",
                table: "Terminals",
                newName: "UX_Terminals_TerminalCode");

            migrationBuilder.RenameIndex(
                name: "IX_TerminalHeartbeats_TerminalId",
                table: "TerminalHeartbeats",
                newName: "UX_TerminalHeartbeats_TerminalId");

            migrationBuilder.RenameIndex(
                name: "IX_Purchases_PublicId",
                table: "Purchases",
                newName: "UX_Purchases_PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_PublicId",
                table: "Orders",
                newName: "UX_Orders_PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_BranchId_OrderNumber",
                table: "Orders",
                newName: "UX_Orders_BranchId_OrderNumber");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryTransactions_PublicId",
                table: "InventoryTransactions",
                newName: "UX_InventoryTransactions_PublicId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_BranchId_PhoneNumber",
                table: "Customers",
                newName: "UX_Customers_BranchId_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                newName: "UX_Categories_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Branches_BranchCode",
                table: "Branches",
                newName: "UX_Branches_BranchCode");

            migrationBuilder.CreateSequence(
                name: "SessionCodeSequence");

            migrationBuilder.AddColumn<string>(
                name: "TerminalTokenHash",
                table: "Terminals",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    TerminalId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Terminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Terminals",
                keyColumn: "Id",
                keyValue: 1,
                column: "TerminalTokenHash",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_BranchId",
                table: "AuditLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TerminalId",
                table: "AuditLogs",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TerminalTokenHash",
                table: "Terminals");

            migrationBuilder.DropSequence(
                name: "SessionCodeSequence");

            migrationBuilder.RenameIndex(
                name: "UX_UserSessions_UserId_Active",
                table: "UserSessions",
                newName: "IX_UserSessions_UserId");

            migrationBuilder.RenameIndex(
                name: "UX_UserSessions_SessionCode",
                table: "UserSessions",
                newName: "IX_UserSessions_SessionCode");

            migrationBuilder.RenameIndex(
                name: "UX_UserSessions_PublicId",
                table: "UserSessions",
                newName: "IX_UserSessions_PublicId");

            migrationBuilder.RenameIndex(
                name: "UX_UserSessionHeartbeats_UserSessionId",
                table: "UserSessionHeartbeats",
                newName: "IX_UserSessionHeartbeats_UserSessionId");

            migrationBuilder.RenameIndex(
                name: "UX_Terminals_TerminalCode",
                table: "Terminals",
                newName: "IX_Terminals_TerminalCode");

            migrationBuilder.RenameIndex(
                name: "UX_TerminalHeartbeats_TerminalId",
                table: "TerminalHeartbeats",
                newName: "IX_TerminalHeartbeats_TerminalId");

            migrationBuilder.RenameIndex(
                name: "UX_Purchases_PublicId",
                table: "Purchases",
                newName: "IX_Purchases_PublicId");

            migrationBuilder.RenameIndex(
                name: "UX_Orders_PublicId",
                table: "Orders",
                newName: "IX_Orders_PublicId");

            migrationBuilder.RenameIndex(
                name: "UX_Orders_BranchId_OrderNumber",
                table: "Orders",
                newName: "IX_Orders_BranchId_OrderNumber");

            migrationBuilder.RenameIndex(
                name: "UX_InventoryTransactions_PublicId",
                table: "InventoryTransactions",
                newName: "IX_InventoryTransactions_PublicId");

            migrationBuilder.RenameIndex(
                name: "UX_Customers_BranchId_PhoneNumber",
                table: "Customers",
                newName: "IX_Customers_BranchId_PhoneNumber");

            migrationBuilder.RenameIndex(
                name: "UX_Categories_Name",
                table: "Categories",
                newName: "IX_Categories_Name");

            migrationBuilder.RenameIndex(
                name: "UX_Branches_BranchCode",
                table: "Branches",
                newName: "IX_Branches_BranchCode");
        }
    }
}
