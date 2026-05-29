using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class HardenPreparationBatchCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PreparationBatches",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "PreparationBatches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TerminalId",
                table: "PreparationBatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserSessionId",
                table: "PreparationBatches",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_TerminalId",
                table: "PreparationBatches",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_UserSessionId",
                table: "PreparationBatches",
                column: "UserSessionId");

            migrationBuilder.CreateIndex(
                name: "UX_PreparationBatches_IdempotencyKey",
                table: "PreparationBatches",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PreparationBatches_Terminals_TerminalId",
                table: "PreparationBatches",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PreparationBatches_UserSessions_UserSessionId",
                table: "PreparationBatches",
                column: "UserSessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreparationBatches_Terminals_TerminalId",
                table: "PreparationBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PreparationBatches_UserSessions_UserSessionId",
                table: "PreparationBatches");

            migrationBuilder.DropIndex(
                name: "IX_PreparationBatches_TerminalId",
                table: "PreparationBatches");

            migrationBuilder.DropIndex(
                name: "IX_PreparationBatches_UserSessionId",
                table: "PreparationBatches");

            migrationBuilder.DropIndex(
                name: "UX_PreparationBatches_IdempotencyKey",
                table: "PreparationBatches");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PreparationBatches");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "PreparationBatches");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "PreparationBatches");

            migrationBuilder.DropColumn(
                name: "UserSessionId",
                table: "PreparationBatches");
        }
    }
}
