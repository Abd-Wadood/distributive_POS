using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class AdminDashboardPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_BranchId_Status",
                table: "UserSessions",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_Status_StartedAt",
                table: "UserSessions",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_BranchId_IsActive",
                table: "Terminals",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TerminalHeartbeats_BranchId_LastSeenAt",
                table: "TerminalHeartbeats",
                columns: new[] { "BranchId", "LastSeenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_CompletedAt_OrderStatus",
                table: "Orders",
                columns: new[] { "BranchId", "CompletedAt", "OrderStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_BranchId_CurrentQuantity",
                table: "Inventories",
                columns: new[] { "BranchId", "CurrentQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BranchId_IsActive",
                table: "AspNetUsers",
                columns: new[] { "BranchId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_BranchId_Status",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_Status_StartedAt",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_Terminals_BranchId_IsActive",
                table: "Terminals");

            migrationBuilder.DropIndex(
                name: "IX_TerminalHeartbeats_BranchId_LastSeenAt",
                table: "TerminalHeartbeats");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId_CompletedAt_OrderStatus",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_BranchId_CurrentQuantity",
                table: "Inventories");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BranchId_IsActive",
                table: "AspNetUsers");
        }
    }
}
