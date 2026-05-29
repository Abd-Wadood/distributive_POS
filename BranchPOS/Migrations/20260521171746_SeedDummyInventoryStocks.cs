using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class SeedDummyInventoryStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH stock_values("InventoryItemId", "StockRoomQuantity", "KitchenQuantity", "AverageUnitCost") AS (
                    VALUES
                        (1, 72.0, 18.0, 90.0),
                        (2, 48.0, 14.0, 140.0),
                        (3, 36.0, 10.0, 190.0),
                        (4, 96.0, 24.0, 65.0),
                        (5, 14.0, 3.0, 550.0),
                        (6, 30.0, 8.0, 420.0),
                        (7, 22.0, 5.0, 380.0),
                        (8, 55.0, 16.0, 1150.0),
                        (9, 40.0, 9.0, 900.0),
                        (10, 18.0, 4.0, 480.0),
                        (11, 70.0, 18.0, 520.0),
                        (12, 28.0, 7.0, 260.0),
                        (13, 250.0, 45.0, 18.0),
                        (14, 220.0, 40.0, 22.0),
                        (15, 300.0, 55.0, 12.0),
                        (16, 500.0, 80.0, 3.0),
                        (17, 65.0, 15.0, 320.0),
                        (18, 32.0, 6.0, 6200.0),
                        (19, 6.0, 1.0, 6500.0),
                        (20, 35.0, 8.0, 220.0),
                        (21, 45.0, 10.0, 120.0),
                        (22, 20.0, 5.0, 470.0),
                        (23, 58.0, 14.0, 650.0),
                        (24, 320.0, 60.0, 10.0),
                        (25, 24.0, 6.0, 360.0),
                        (26, 38.0, 9.0, 540.0),
                        (27, 26.0, 7.0, 1450.0),
                        (28, 44.0, 11.0, 780.0),
                        (29, 52.0, 13.0, 300.0),
                        (30, 34.0, 8.0, 1300.0),
                        (31, 32.0, 8.0, 430.0),
                        (32, 50.0, 12.0, 580.0),
                        (33, 180.0, 35.0, 25.0),
                        (34, 25.0, 5.0, 210.0),
                        (35, 240.0, 42.0, 16.0),
                        (36, 38.0, 9.0, 720.0),
                        (37, 28.0, 7.0, 340.0),
                        (38, 700.0, 120.0, 2.0),
                        (39, 20.0, 4.0, 160.0),
                        (40, 42.0, 10.0, 240.0),
                        (41, 90.0, 20.0, 85.0),
                        (42, 30.0, 6.0, 180.0),
                        (43, 36.0, 8.0, 260.0)
                )
                INSERT INTO "InventoryStocks" ("BranchId", "InventoryItemId", "InventoryLocationId", "Quantity", "AverageUnitCost", "UpdatedAt")
                SELECT 1, stock_values."InventoryItemId", location_stock."InventoryLocationId", location_stock."Quantity", stock_values."AverageUnitCost", TIMESTAMPTZ '2026-01-01T00:00:00Z'
                FROM stock_values
                CROSS JOIN LATERAL (
                    VALUES
                        (1, stock_values."StockRoomQuantity"),
                        (2, stock_values."KitchenQuantity")
                ) AS location_stock("InventoryLocationId", "Quantity")
                ON CONFLICT ("InventoryItemId", "InventoryLocationId") DO UPDATE
                SET
                    "BranchId" = EXCLUDED."BranchId",
                    "Quantity" = EXCLUDED."Quantity",
                    "AverageUnitCost" = EXCLUDED."AverageUnitCost",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank so rolling back does not delete or overwrite live stock counts.
        }
    }
}
