using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class SafeSessionShiftLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_TerminalId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_UserId_Active",
                table: "UserSessions");

            migrationBuilder.AddColumn<decimal>(
                name: "CashDifference",
                table: "UserSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "UserSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosingRequestedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CountedClosingCash",
                table: "UserSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedClosingCash",
                table: "UserSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningCashAmount",
                table: "UserSessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "UserSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenedByUserId",
                table: "UserSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManagerApproval",
                table: "UserSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Closed' WHERE \"Status\" = 'Ended';");
            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Abandoned' WHERE \"Status\" = 'Interrupted';");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ClosedByUserId",
                table: "UserSessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_ReopenedByUserId",
                table: "UserSessions",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_TerminalId_Active",
                table: "UserSessions",
                column: "TerminalId",
                unique: true,
                filter: "\"Status\" IN ('Active', 'Reopened', 'ClosingPending')");

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_UserId_Active",
                table: "UserSessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\" IN ('Active', 'Reopened', 'ClosingPending')");

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_UserId_BranchId_Active",
                table: "UserSessions",
                columns: new[] { "UserId", "BranchId" },
                unique: true,
                filter: "\"Status\" IN ('Active', 'Reopened', 'ClosingPending')");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_AspNetUsers_ClosedByUserId",
                table: "UserSessions",
                column: "ClosedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_AspNetUsers_ReopenedByUserId",
                table: "UserSessions",
                column: "ReopenedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_AspNetUsers_ClosedByUserId",
                table: "UserSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_AspNetUsers_ReopenedByUserId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_ClosedByUserId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_ReopenedByUserId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_TerminalId_Active",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_UserId_Active",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "UX_UserSessions_UserId_BranchId_Active",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "CashDifference",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ClosingRequestedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "CountedClosingCash",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ExpectedClosingCash",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "OpeningCashAmount",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ReopenedByUserId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RequiresManagerApproval",
                table: "UserSessions");

            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Ended' WHERE \"Status\" = 'Closed';");
            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Interrupted' WHERE \"Status\" = 'Abandoned';");
            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Active' WHERE \"Status\" IN ('Reopened', 'ClosingPending');");
            migrationBuilder.Sql("UPDATE \"UserSessions\" SET \"Status\" = 'Ended' WHERE \"Status\" = 'ForceClosed';");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_TerminalId",
                table: "UserSessions",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "UX_UserSessions_UserId_Active",
                table: "UserSessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Active'");
        }
    }
}
