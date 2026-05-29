using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class CrashSafeInventoryTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_TerminalId",
                table: "IdempotencyRecords");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "InventoryMovements",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceId",
                table: "IdempotencyRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "IdempotencyRecords",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserSessionId",
                table: "IdempotencyRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Terminal_IdempotencyKey",
                table: "InventoryMovements",
                columns: new[] { "TerminalId", "IdempotencyKey" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_InventoryMovements_DuplicateProtection"
                ON "InventoryMovements" (
                    "ReferenceType",
                    "ReferenceId",
                    "MovementType",
                    "InventoryItemId",
                    COALESCE("FromLocationId", 0),
                    COALESCE("ToLocationId", 0),
                    "IdempotencyKey"
                )
                WHERE "ReferenceType" IS NOT NULL
                  AND "ReferenceId" IS NOT NULL
                  AND "IdempotencyKey" IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_QuantityBase_Positive",
                table: "InventoryMovements",
                sql: "\"QuantityBase\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserSessionId",
                table: "IdempotencyRecords",
                column: "UserSessionId");

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRecords_Terminal_Key_Reference",
                table: "IdempotencyRecords",
                columns: new[] { "TerminalId", "IdempotencyKey", "ReferenceType" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyRecords_UserSessions_UserSessionId",
                table: "IdempotencyRecords",
                column: "UserSessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyRecords_UserSessions_UserSessionId",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Terminal_IdempotencyKey",
                table: "InventoryMovements");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_InventoryMovements_DuplicateProtection";""");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_QuantityBase_Positive",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_UserSessionId",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "UX_IdempotencyRecords_Terminal_Key_Reference",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "UserSessionId",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TerminalId",
                table: "IdempotencyRecords",
                column: "TerminalId");
        }
    }
}
