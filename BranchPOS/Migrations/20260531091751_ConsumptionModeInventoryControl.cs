using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class ConsumptionModeInventoryControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExpenseOnly",
                table: "PurchaseItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PurchaseItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectInventoryItemId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DirectQuantityBase",
                table: "Products",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowKitchenDispatch",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualConsumption",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRecipeConsumption",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BatchTrackingRequired",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ConsumptionMode",
                table: "InventoryItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ManualKitchenIssue");

            migrationBuilder.AddColumn<bool>(
                name: "ExpiryTrackingRequired",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpenseOnly",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStockTracked",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumKitchenLevel",
                table: "InventoryItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePurchaseConversion",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrackingLevel",
                table: "InventoryItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.CreateTable(
                name: "ManualKitchenUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    UsageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserSessionId = table.Column<int>(type: "integer", nullable: true),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    OpeningKitchenQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedFromStockRoomQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingKitchenQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    WastedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualUsedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualKitchenUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualKitchenUsages_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualKitchenUsages_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualKitchenUsages_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualKitchenUsages_UserSessions_UserSessionId",
                        column: x => x.UserSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StockCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CountDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LocationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DifferenceQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockCounts_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCounts_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 34,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 39,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 41,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 42,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 43,
                columns: new[] { "AllowKitchenDispatch", "AllowManualConsumption", "AllowRecipeConsumption", "BatchTrackingRequired", "ConsumptionMode", "ExpiryTrackingRequired", "IsStockTracked", "MaximumKitchenLevel", "RequirePurchaseConversion", "TrackingLevel" },
                values: new object[] { true, true, false, false, "ManualKitchenIssue", false, true, null, false, "Medium" });

            migrationBuilder.Sql("""
                UPDATE "InventoryItems"
                SET
                    "ConsumptionMode" = CASE
                        WHEN "Name" ILIKE '%coke%' OR "Name" ILIKE '%coca%' OR "Name" ILIKE '%pepsi%' OR "Name" ILIKE '%sprite%' OR "Name" ILIKE '%7up%' OR "Name" ILIKE '%water%' OR "Name" ILIKE '%juice%' THEN 'DirectSale'
                        WHEN "Name" ILIKE '%chicken%' OR "Name" ILIKE '%cheese%' OR "Name" ILIKE '%mayo%' OR "Name" ILIKE '%pizza sauce%' OR "Name" ILIKE '%dough%' OR "Name" ILIKE '%fries%' OR "Name" ILIKE '%patty%' OR "Name" ILIKE '%nugget%' OR "Name" ILIKE '%fish%' OR "Name" ILIKE '%tikka%' OR "Name" ILIKE '%boti%' OR "Name" ILIKE '%kabab%' OR "Name" ILIKE '%sausage%' OR "Name" ILIKE '%pepperoni%' OR "Name" ILIKE '%sauce%' THEN 'RecipeConsumption'
                        WHEN "Name" ILIKE '%sweet corn%' OR "Name" ILIKE '%olive%' OR "Name" ILIKE '%mushroom%' OR "Name" ILIKE '%jalapeno%' OR "Name" ILIKE '%foil%' OR "Name" ILIKE '%food bag%' OR "Name" ILIKE '%fork%' OR "Name" ILIKE '%packing%' THEN 'ManualKitchenIssue'
                        WHEN "Name" ILIKE '%tape%' OR "Name" ILIKE '%tissue%' OR "Name" ILIKE '%printer roll%' OR "Name" ILIKE '%cling film%' THEN 'PeriodicCount'
                        WHEN "Name" ILIKE '%gas cylinder%' OR "Name" ILIKE '%cleaning%' OR "Name" ILIKE '%repair%' OR "Name" ILIKE '%utility%' THEN 'ExpenseOnly'
                        ELSE 'ManualKitchenIssue'
                    END;

                UPDATE "InventoryItems"
                SET
                    "IsExpenseOnly" = "ConsumptionMode" = 'ExpenseOnly',
                    "IsStockTracked" = "ConsumptionMode" <> 'ExpenseOnly',
                    "AllowRecipeConsumption" = "ConsumptionMode" = 'RecipeConsumption',
                    "AllowManualConsumption" = "ConsumptionMode" = 'ManualKitchenIssue',
                    "AllowKitchenDispatch" = "ConsumptionMode" IN ('RecipeConsumption', 'ManualKitchenIssue'),
                    "RequirePurchaseConversion" = "ConsumptionMode" IN ('RecipeConsumption', 'DirectSale'),
                    "TrackingLevel" = CASE
                        WHEN "ConsumptionMode" IN ('RecipeConsumption', 'DirectSale') THEN 'High'
                        WHEN "ConsumptionMode" = 'ManualKitchenIssue' THEN 'Medium'
                        ELSE 'Low'
                    END,
                    "BaseUnit" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN 'None' ELSE "BaseUnit" END,
                    "PurchaseUnitName" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN NULL ELSE "PurchaseUnitName" END,
                    "DefaultConversionFactorToBase" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN NULL ELSE "DefaultConversionFactorToBase" END,
                    "ReorderLevel" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN 0 ELSE "ReorderLevel" END,
                    "MinimumKitchenLevel" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN NULL ELSE "MinimumKitchenLevel" END,
                    "MaximumKitchenLevel" = CASE WHEN "ConsumptionMode" = 'ExpenseOnly' THEN NULL ELSE "MaximumKitchenLevel" END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_DirectInventoryItemId",
                table: "Products",
                column: "DirectInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualKitchenUsages_BranchId_UsageDate",
                table: "ManualKitchenUsages",
                columns: new[] { "BranchId", "UsageDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualKitchenUsages_CreatedByUserId",
                table: "ManualKitchenUsages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualKitchenUsages_InventoryItemId",
                table: "ManualKitchenUsages",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualKitchenUsages_UserSessionId",
                table: "ManualKitchenUsages",
                column: "UserSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_BranchId_CountDate",
                table: "StockCounts",
                columns: new[] { "BranchId", "CountDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_CreatedByUserId",
                table: "StockCounts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_Item_Location",
                table: "StockCounts",
                columns: new[] { "InventoryItemId", "LocationType" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_InventoryItems_DirectInventoryItemId",
                table: "Products",
                column: "DirectInventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_InventoryItems_DirectInventoryItemId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ManualKitchenUsages");

            migrationBuilder.DropTable(
                name: "StockCounts");

            migrationBuilder.DropIndex(
                name: "IX_Products_DirectInventoryItemId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsExpenseOnly",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "DirectInventoryItemId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DirectQuantityBase",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AllowKitchenDispatch",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AllowManualConsumption",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "AllowRecipeConsumption",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "BatchTrackingRequired",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ConsumptionMode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "ExpiryTrackingRequired",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsExpenseOnly",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "IsStockTracked",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "MaximumKitchenLevel",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RequirePurchaseConversion",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TrackingLevel",
                table: "InventoryItems");
        }
    }
}
