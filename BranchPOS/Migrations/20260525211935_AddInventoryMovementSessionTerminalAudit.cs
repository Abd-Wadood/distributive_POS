using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMovementSessionTerminalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "InventoryMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserSessionId",
                table: "InventoryMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_TerminalId",
                table: "InventoryMovements",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_UserSessionId",
                table: "InventoryMovements",
                column: "UserSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Terminals_TerminalId",
                table: "InventoryMovements",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_UserSessions_UserSessionId",
                table: "InventoryMovements",
                column: "UserSessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Terminals_TerminalId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_UserSessions_UserSessionId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_TerminalId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_UserSessionId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "UserSessionId",
                table: "InventoryMovements");
        }
    }
}
