using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class DbBackedIdempotencyProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases");

            migrationBuilder.AddColumn<string>(
                name: "CloseIdempotencyKey",
                table: "UserSessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "UserSessions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Purchases",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Purchases",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "InventoryTransactions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    TerminalId = table.Column<int>(type: "integer", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResourceId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ResponseCode = table.Column<int>(type: "integer", nullable: true),
                    ResponseBodySummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_Terminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "Terminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_CloseIdempotencyKey",
                table: "UserSessions",
                column: "CloseIdempotencyKey",
                unique: true,
                filter: "\"CloseIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_IdempotencyKey",
                table: "UserSessions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Purchases_IdempotencyKey",
                table: "Purchases",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Purchases_SupplierId_InvoiceNumber",
                table: "Purchases",
                columns: new[] { "SupplierId", "InvoiceNumber" },
                unique: true,
                filter: "\"InvoiceNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Orders_IdempotencyKey",
                table: "Orders",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryTransactions_IdempotencyKey",
                table: "InventoryTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_BranchId",
                table: "IdempotencyRecords",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ExpiresAt",
                table: "IdempotencyRecords",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Status_CreatedAt",
                table: "IdempotencyRecords",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TerminalId",
                table: "IdempotencyRecords",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserId",
                table: "IdempotencyRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRecords_IdempotencyKey",
                table: "IdempotencyRecords",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRecords_OperationType_IdempotencyKey",
                table: "IdempotencyRecords",
                columns: new[] { "OperationType", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_CloseIdempotencyKey",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_IdempotencyKey",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_Purchases_IdempotencyKey",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "UX_Purchases_SupplierId_InvoiceNumber",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "UX_Orders_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_InventoryTransactions_IdempotencyKey",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "CloseIdempotencyKey",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "InventoryTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases",
                column: "SupplierId");
        }
    }
}
