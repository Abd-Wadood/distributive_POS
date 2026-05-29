# Stock Management Report

## Scope

This report explains how stock is managed in the BranchPOS application based on the current codebase. Stock management is handled through inventory items, physical locations, stock balances, stock movements, purchases, kitchen requests, recipes, and POS order completion.

## Core Stock Model

### Inventory Items

`InventoryItem` represents an ingredient, packaging item, drink, or other stock-controlled item. Each item belongs to a branch and has:

- `Name`: the stock item name, such as Cheese, Cooking Oil, or Coca-Cola.
- `BaseUnit`: the unit used internally for stock calculation, such as Piece, Gram, or ML.
- `PurchaseUnitName`: the unit used when buying stock, such as Kg, Liter, Packet, Bottle, Roll, or Tin.
- `DefaultConversionFactorToBase`: converts purchase units into base units.
- `ReorderLevel`: the threshold used for low-stock reporting.
- `IsActive`: allows old items to be deactivated without deleting stock history.

Example: if Cooking Oil has `BaseUnit = ML`, `PurchaseUnitName = Liter`, and `DefaultConversionFactorToBase = 1000`, then buying 5 liters creates 5000 ML of stock.

### Inventory Locations

`InventoryLocation` represents where stock is held inside a branch. The system currently uses two main locations:

- `Stock Room`: main storage where purchases are received.
- `Kitchen`: operational stock used by POS sales.

Locations are branch-specific. If a Stock Room or Kitchen location does not exist, services create it automatically when needed.

### Inventory Stock

`InventoryStock` stores the current balance for one inventory item at one location. It contains:

- `InventoryItemId`
- `InventoryLocationId`
- `QuantityBase`
- `AverageUnitCostBase`

The database enforces one stock row per item/location pair and prevents negative stock through a check constraint on `QuantityBase`.

### Inventory Movements

`InventoryMovement` is the audit trail for stock changes. Every important stock operation creates a movement record with:

- item
- source location
- destination location
- quantity in base units
- unit cost
- total cost
- movement type
- reference document
- user who performed it
- timestamp

Supported movement types are:

- `Purchase`
- `Transfer`
- `Consumption`
- `Waste`
- `Adjustment`

In the current implemented flows, purchases, transfers, and consumption are actively used.

## Main Stock Lifecycle

### 1. Stock Item Setup

A StockManager or Admin manages inventory items from the Inventory Items screen.

The item setup defines how stock will be counted and purchased. This is important because all later calculations use the base unit.

For example:

- Cheese can be purchased as Kg but consumed as Gram.
- Cooking Oil can be purchased as Liter but consumed as ML.
- Drinks can be purchased and sold as Piece.

Inactive inventory items are hidden from new purchases, recipes, and kitchen requests, but their old records remain in the database.

### 2. Product Recipe Setup

Recipes connect sellable products to inventory usage.

Each active product should have one active recipe. A recipe contains one or more `RecipeIngredient` rows. Each ingredient defines:

- which inventory item is consumed
- how much is required per product sold
- the quantity in base units
- optional display quantity/unit for easier entry

Example:

If a burger recipe uses:

- 1 Chicken Patty
- 80 Gram Cheese
- 30 ML Sauce

Then selling 2 burgers requires:

- 2 Chicken Patties
- 160 Gram Cheese
- 60 ML Sauce

The POS system uses these recipe quantities to check availability and deduct stock.

### 3. Purchase Receiving

Purchases are handled by the `PurchaseService`.

Only a StockManager with an active stock session on a registered terminal can create purchases. This prevents stock from being added without a valid user, branch, session, and terminal context.

When a purchase is created:

1. The purchase request is validated.
2. The system confirms all selected inventory items belong to the active branch.
3. Each purchase quantity is converted into base units.
4. A `Purchase` and its `PurchaseItem` rows are saved.
5. The purchased quantities are added to the Stock Room.
6. The item's average base-unit cost is recalculated using weighted average costing.
7. A `Purchase` inventory movement is created for each item.

Example:

If the Stock Room has 1000 Gram of Cheese at Rs. 2 per Gram and a new purchase adds 1000 Gram at Rs. 3 per Gram, the new average cost becomes Rs. 2.50 per Gram.

The purchase service also uses idempotency keys, so repeated form submissions do not accidentally duplicate stock.

### 4. Kitchen Request

The kitchen does not consume directly from the Stock Room. Stock must be transferred from Stock Room to Kitchen through a kitchen request.

Kitchen requests have this lifecycle:

1. `Pending`: created with requested inventory items and quantities.
2. `Approved`: approved quantities are set by the StockManager/Admin.
3. `Rejected`: pending request is rejected.
4. `Dispatched`: approved stock is moved from Stock Room to Kitchen.

When a request is dispatched:

1. The system verifies the request is approved.
2. It locks the Stock Room stock rows.
3. It checks that Stock Room has enough quantity.
4. It deducts from Stock Room.
5. It adds to Kitchen.
6. It carries the cost into Kitchen using weighted average costing.
7. It creates a `Transfer` movement.
8. It records dispatched quantities and dispatch time.

This design separates storage stock from production stock. POS sales can only consume from Kitchen stock, not directly from Stock Room.

### 5. POS Availability Check

The POS menu availability is handled by `ProductAvailabilityService`.

For each product, the system checks:

- product is active
- product has recipe requirements
- Kitchen has enough stock for every recipe ingredient

If any ingredient is missing or below the quantity needed to make one product, the product is marked unavailable.

The product and recipe menu structure is cached for a short period, but live Kitchen stock quantities are not cached. This keeps availability checks close to real stock while avoiding repeated menu queries.

### 6. Order Completion and Stock Consumption

Stock is deducted only when an order is finalized as completed. Draft orders do not consume stock.

When a cashier finalizes an order:

1. The system validates the active cashier session and terminal.
2. It loads the ordered products and their active recipes.
3. It calculates total required ingredients across all ordered items.
4. It locks the required Kitchen stock rows in a consistent order.
5. It validates that Kitchen stock is sufficient.
6. It saves the completed order and order items.
7. It deducts required ingredient quantities from Kitchen stock.
8. It creates a `Consumption` movement for each consumed inventory item.
9. It records cost using the Kitchen stock average unit cost.

Example:

If an order has:

- 2 burgers using 80 Gram Cheese each
- 1 pizza using 120 Gram Cheese

The system groups the requirement and deducts 280 Gram Cheese in one stock calculation for that order.

The order finalization runs inside a serializable database transaction and uses row locking to reduce overselling under concurrent cashier activity.

## Costing Method

The application uses weighted average costing at the inventory stock row level.

Average unit cost is updated when stock enters a location:

- purchase into Stock Room
- transfer into Kitchen

Consumption uses the current Kitchen `AverageUnitCostBase` to calculate `TotalCost`.

Profit reporting uses:

- completed order revenue
- inventory consumption cost
- operational expenses

Net profit is calculated as:

`Sales Revenue - Ingredient Cost - Operational Expenses`

## Reporting

The Inventory Reports area provides these stock views:

### Stock Room Report

Shows current stock in the Stock Room location, including item, unit, quantity, and average cost.

### Kitchen Report

Shows current stock in the Kitchen location. This is the stock available for POS product preparation.

### Low Stock Report

Shows stock rows where:

`QuantityBase <= InventoryItem.ReorderLevel`

This report checks stock per location. An item can be low in Kitchen even if Stock Room has enough quantity.

### Movements Report

Shows the latest 500 inventory movements, including purchases, transfers, and consumption. This is the main audit trail for stock history.

### Profit Report

Combines completed order revenue, consumption movement costs, and operational expenses to estimate net profit.

## Roles and Access Control

Stock-related screens are protected by roles:

- `StockManager`: can manage purchases, inventory items, reports, recipes, and kitchen requests.
- `Admin`: can access many inventory/reporting functions, including kitchen requests and inventory reports.
- `Cashier`: finalizes POS orders, which indirectly consumes Kitchen stock.

Purchase creation is stricter than normal screen access. It requires:

- active StockManager session
- active registered terminal
- matching branch/session/terminal identity

Order finalization requires:

- active Cashier session
- active registered terminal
- matching branch/session/terminal identity

## Data Integrity Controls

The system uses several protections to keep stock consistent:

- Branch scoping prevents one branch from using another branch's inventory items.
- Unique stock rows prevent duplicate item/location balances.
- Database check constraint prevents negative `InventoryStock.QuantityBase`.
- Row-level database locks are used before stock mutation.
- Purchase and order idempotency prevent duplicate stock effects from repeated requests.
- Order finalization uses a serializable transaction.
- Recipe validation blocks sale completion if a product has no active recipe.
- Kitchen stock validation blocks order completion when ingredients are insufficient.
- Stock Room validation blocks kitchen dispatch when storage stock is insufficient.

## Current Stock Flow Summary

The stock flow is:

1. Create inventory items with base units and purchase conversions.
2. Create recipes that map products to inventory requirements.
3. Receive purchases into Stock Room.
4. Transfer approved stock from Stock Room to Kitchen through kitchen requests.
5. POS checks availability from Kitchen stock.
6. Completed orders consume recipe ingredients from Kitchen.
7. Inventory movements record every purchase, transfer, and consumption.
8. Reports use current stock and movement history for visibility and profit analysis.

## Important Observations

- POS sales consume only Kitchen stock. If Stock Room has stock but Kitchen does not, products can still be unavailable.
- Draft orders do not reserve or deduct stock.
- Recipes are mandatory for completed sales. A product without a recipe cannot be finalized.
- Average cost is location-based. Stock Room and Kitchen each maintain their own average unit cost.
- Waste and adjustment movement types exist in the enum, but this report found no active service flow implementing waste or manual adjustment screens.
- The low-stock report is based on current stock rows. Items with no stock row at a location may not appear until a stock row exists.
- Inventory quantities are stored in base units with three decimal places of precision.

## Recommended Operational Process

1. Maintain accurate inventory item base units and conversion factors before receiving purchases.
2. Keep product recipes updated whenever menu items or portions change.
3. Receive all supplier stock through Purchases so cost and movement history are captured.
4. Transfer stock to Kitchen through approved kitchen requests before selling.
5. Review Low Stock daily for both Stock Room and Kitchen.
6. Use Movements report to audit unexpected stock changes.
7. Compare Profit report ingredient cost against purchases and operational expenses regularly.

