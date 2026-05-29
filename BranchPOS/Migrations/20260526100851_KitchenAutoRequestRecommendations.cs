using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class KitchenAutoRequestRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KitchenRequestDetails_InventoryItemId",
                table: "KitchenRequestDetails");

            migrationBuilder.AddColumn<string>(
                name: "AutoReason",
                table: "KitchenRequests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "CreatedBySessionId",
                table: "KitchenRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByTerminalId",
                table: "KitchenRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispatchedByUserId",
                table: "KitchenRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KitchenLocationId",
                table: "KitchenRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerNotes",
                table: "KitchenRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestSource",
                table: "KitchenRequests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "KitchenRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "KitchenRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentKitchenQuantityAtRequest",
                table: "KitchenRequestDetails",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "KitchenLocationId",
                table: "KitchenRequestDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumKitchenLevelAtRequest",
                table: "KitchenRequestDetails",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingRequestQuantity",
                table: "KitchenRequestDetails",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedQuantity",
                table: "KitchenRequestDetails",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RequestSource",
                table: "KitchenRequestDetails",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "KitchenRequestDetails",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "PendingManagerReview");

            migrationBuilder.AddColumn<decimal>(
                name: "StockRoomAvailableAtRequest",
                table: "KitchenRequestDetails",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "KitchenRequestDetailId",
                table: "InventoryMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumKitchenLevel",
                table: "InventoryItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 11,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 12,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 13,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 14,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 15,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 16,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 17,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 18,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 19,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 20,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 21,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 22,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 23,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 24,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 25,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 26,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 27,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 28,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 29,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 30,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 31,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 32,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 33,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 34,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 35,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 36,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 37,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 38,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 39,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 40,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 41,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 42,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 43,
                column: "MinimumKitchenLevel",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_CreatedBySessionId",
                table: "KitchenRequests",
                column: "CreatedBySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_CreatedByTerminalId",
                table: "KitchenRequests",
                column: "CreatedByTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_DispatchedByUserId",
                table: "KitchenRequests",
                column: "DispatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_Kitchen_Status_Source",
                table: "KitchenRequests",
                columns: new[] { "KitchenLocationId", "Status", "RequestSource" });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_ReviewedByUserId",
                table: "KitchenRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequests_Source_Status",
                table: "KitchenRequests",
                columns: new[] { "RequestSource", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequestDetails_Item_Status",
                table: "KitchenRequestDetails",
                columns: new[] { "InventoryItemId", "Status" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_KitchenRequestDetails_ActiveAuto_Item_Kitchen"
                ON "KitchenRequestDetails" (
                    COALESCE("KitchenLocationId", 0),
                    "InventoryItemId",
                    "RequestSource"
                )
                WHERE "RequestSource" = 'Auto'
                  AND "Status" IN ('PendingManagerReview', 'Approved');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_KitchenRequestDetailId",
                table: "InventoryMovements",
                column: "KitchenRequestDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_KitchenRequestDetails_KitchenRequestDeta~",
                table: "InventoryMovements",
                column: "KitchenRequestDetailId",
                principalTable: "KitchenRequestDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequestDetails_InventoryLocations_KitchenLocationId",
                table: "KitchenRequestDetails",
                column: "KitchenLocationId",
                principalTable: "InventoryLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequests_AspNetUsers_DispatchedByUserId",
                table: "KitchenRequests",
                column: "DispatchedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequests_AspNetUsers_ReviewedByUserId",
                table: "KitchenRequests",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequests_InventoryLocations_KitchenLocationId",
                table: "KitchenRequests",
                column: "KitchenLocationId",
                principalTable: "InventoryLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequests_Terminals_CreatedByTerminalId",
                table: "KitchenRequests",
                column: "CreatedByTerminalId",
                principalTable: "Terminals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_KitchenRequests_UserSessions_CreatedBySessionId",
                table: "KitchenRequests",
                column: "CreatedBySessionId",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_KitchenRequestDetails_KitchenRequestDeta~",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequestDetails_InventoryLocations_KitchenLocationId",
                table: "KitchenRequestDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequests_AspNetUsers_DispatchedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequests_AspNetUsers_ReviewedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequests_InventoryLocations_KitchenLocationId",
                table: "KitchenRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequests_Terminals_CreatedByTerminalId",
                table: "KitchenRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_KitchenRequests_UserSessions_CreatedBySessionId",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_CreatedBySessionId",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_CreatedByTerminalId",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_DispatchedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_Kitchen_Status_Source",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_ReviewedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequests_Source_Status",
                table: "KitchenRequests");

            migrationBuilder.DropIndex(
                name: "IX_KitchenRequestDetails_Item_Status",
                table: "KitchenRequestDetails");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_KitchenRequestDetails_ActiveAuto_Item_Kitchen";""");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_KitchenRequestDetailId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "AutoReason",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "CreatedBySessionId",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByTerminalId",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "DispatchedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "KitchenLocationId",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "ManagerNotes",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "RequestSource",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "KitchenRequests");

            migrationBuilder.DropColumn(
                name: "CurrentKitchenQuantityAtRequest",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "KitchenLocationId",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "MinimumKitchenLevelAtRequest",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "PendingRequestQuantity",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "RecommendedQuantity",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "RequestSource",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "StockRoomAvailableAtRequest",
                table: "KitchenRequestDetails");

            migrationBuilder.DropColumn(
                name: "KitchenRequestDetailId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "MinimumKitchenLevel",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenRequestDetails_InventoryItemId",
                table: "KitchenRequestDetails",
                column: "InventoryItemId");
        }
    }
}
