using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class OrderStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryState",
                table: "Orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantityBase",
                table: "InventoryStocks",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "OrderInventoryReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    InventoryStockId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    InventoryLocationId = table.Column<int>(type: "integer", nullable: false),
                    RequiredQuantityBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WastedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderInventoryReservations", x => x.Id);
                    table.CheckConstraint("CK_OrderInventoryReservations_RequiredQuantity_Positive", "\"RequiredQuantityBase\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderInventoryReservations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderInventoryReservations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderInventoryReservations_InventoryLocations_InventoryLoca~",
                        column: x => x.InventoryLocationId,
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderInventoryReservations_InventoryStocks_InventoryStockId",
                        column: x => x.InventoryStockId,
                        principalTable: "InventoryStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderInventoryReservations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Branch_Status_InventoryState",
                table: "Orders",
                columns: new[] { "BranchId", "OrderStatus", "InventoryState" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CancelledByUserId",
                table: "Orders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Orders_BranchId_ClientRequestId",
                table: "Orders",
                columns: new[] { "BranchId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_QuantityBase_Covers_Reserved",
                table: "InventoryStocks",
                sql: "\"QuantityBase\" >= \"ReservedQuantityBase\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_ReservedQuantityBase_NonNegative",
                table: "InventoryStocks",
                sql: "\"ReservedQuantityBase\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_OrderInventoryReservations_BranchId",
                table: "OrderInventoryReservations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderInventoryReservations_InventoryLocationId",
                table: "OrderInventoryReservations",
                column: "InventoryLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderInventoryReservations_Item_Status",
                table: "OrderInventoryReservations",
                columns: new[] { "InventoryItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderInventoryReservations_Order_Status",
                table: "OrderInventoryReservations",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderInventoryReservations_Stock_Status",
                table: "OrderInventoryReservations",
                columns: new[] { "InventoryStockId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_CancelledByUserId",
                table: "Orders",
                column: "CancelledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_CancelledByUserId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderInventoryReservations");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Branch_Status_InventoryState",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CancelledByUserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_Orders_BranchId_ClientRequestId",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryStocks_QuantityBase_Covers_Reserved",
                table: "InventoryStocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryStocks_ReservedQuantityBase_NonNegative",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InventoryState",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReservedQuantityBase",
                table: "InventoryStocks");
        }
    }
}
