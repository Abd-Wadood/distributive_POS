using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class InventoryBaseUnitConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryStocks_Quantity_NonNegative",
                table: "InventoryStocks");

            migrationBuilder.RenameColumn(
                name: "QuantityRequired",
                table: "RecipeIngredients",
                newName: "QuantityRequiredBase");

            migrationBuilder.RenameColumn(
                name: "UnitCost",
                table: "PurchaseItems",
                newName: "UnitCostPerPurchaseUnit");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "PurchaseItems",
                newName: "PurchaseQuantity");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "InventoryStocks",
                newName: "QuantityBase");

            migrationBuilder.RenameColumn(
                name: "AverageUnitCost",
                table: "InventoryStocks",
                newName: "AverageUnitCostBase");

            migrationBuilder.RenameColumn(
                name: "UnitCost",
                table: "InventoryMovements",
                newName: "UnitCostBase");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "InventoryMovements",
                newName: "QuantityBase");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "InventoryItems",
                newName: "BaseUnit");

            migrationBuilder.RenameIndex(
                name: "UX_InventoryItems_BranchId_Name_Unit",
                table: "InventoryItems",
                newName: "UX_InventoryItems_BranchId_Name_BaseUnit");

            migrationBuilder.AddColumn<decimal>(
                name: "DisplayQuantity",
                table: "RecipeIngredients",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayUnit",
                table: "RecipeIngredients",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseQuantity",
                table: "PurchaseItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactorToBase",
                table: "PurchaseItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseUnitName",
                table: "PurchaseItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "PurchaseItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostBase",
                table: "PurchaseItems",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultConversionFactorToBase",
                table: "InventoryItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseUnitName",
                table: "InventoryItems",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "Name", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Coca-Cola 0.5L", "0.5L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "Name", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Coca-Cola 1L", "1L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "Name", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Coca-Cola 1.5L", "1.5L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "Name", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Coca-Cola 300ML", "300ML Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Roll" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Bottle", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Tin", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Kg", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Roll" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Liter", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Tin", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Refill" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "Name", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Jalapeno", "Jar", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Liter", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Tin", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Liter", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Kg", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Bottle", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "ML", 1000m, "Liter", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 34,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Roll" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Tin", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { 1m, "Piece" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 39,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Roll" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 41,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName" },
                values: new object[] { "Piece", 1m, "Roll" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 42,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 43,
                columns: new[] { "BaseUnit", "DefaultConversionFactorToBase", "PurchaseUnitName", "ReorderLevel" },
                values: new object[] { "Gram", 1000m, "Packet", 1000m });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_QuantityBase_NonNegative",
                table: "InventoryStocks",
                sql: "\"QuantityBase\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryStocks_QuantityBase_NonNegative",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "DisplayQuantity",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "DisplayUnit",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactorToBase",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitName",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "UnitCostBase",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "DefaultConversionFactorToBase",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitName",
                table: "InventoryItems");

            migrationBuilder.RenameColumn(
                name: "QuantityRequiredBase",
                table: "RecipeIngredients",
                newName: "QuantityRequired");

            migrationBuilder.RenameColumn(
                name: "UnitCostPerPurchaseUnit",
                table: "PurchaseItems",
                newName: "UnitCost");

            migrationBuilder.RenameColumn(
                name: "PurchaseQuantity",
                table: "PurchaseItems",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "QuantityBase",
                table: "InventoryStocks",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "AverageUnitCostBase",
                table: "InventoryStocks",
                newName: "AverageUnitCost");

            migrationBuilder.RenameColumn(
                name: "UnitCostBase",
                table: "InventoryMovements",
                newName: "UnitCost");

            migrationBuilder.RenameColumn(
                name: "QuantityBase",
                table: "InventoryMovements",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "BaseUnit",
                table: "InventoryItems",
                newName: "Unit");

            migrationBuilder.RenameIndex(
                name: "UX_InventoryItems_BranchId_Name_BaseUnit",
                table: "InventoryItems",
                newName: "UX_InventoryItems_BranchId_Name_Unit");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Unit" },
                values: new object[] { "Coca-Cola", "0.5L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Name", "Unit" },
                values: new object[] { "Coca-Cola", "1L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Name", "Unit" },
                values: new object[] { "Coca-Cola", "1.5L Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Name", "Unit" },
                values: new object[] { "Coca-Cola", "300ML Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "Unit",
                value: "Roll");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Tin" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Kg" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "Unit",
                value: "Packet");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "Unit",
                value: "Roll");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Liter" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Tin" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 19,
                column: "Unit",
                value: "Refill");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "Name", "ReorderLevel", "Unit" },
                values: new object[] { "Jalapeño", 10m, "Jar" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Liter" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Tin" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Liter" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 28,
                column: "Unit",
                value: "Packet");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 29,
                column: "Unit",
                value: "Packet");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Kg" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Bottle" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Liter" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 34,
                column: "Unit",
                value: "Roll");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 36,
                column: "Unit",
                value: "Packet");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Tin" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 39,
                column: "Unit",
                value: "Roll");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 41,
                column: "Unit",
                value: "Roll");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 42,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: 43,
                columns: new[] { "ReorderLevel", "Unit" },
                values: new object[] { 10m, "Packet" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_Quantity_NonNegative",
                table: "InventoryStocks",
                sql: "\"Quantity\" >= 0");
        }
    }
}
