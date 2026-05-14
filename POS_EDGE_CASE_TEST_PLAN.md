# POS Edge Case Test Plan

Use this checklist with a disposable PostgreSQL database or a clearly isolated test branch. Do not run destructive setup against production data.

## Inventory Concurrency

### Two Terminals Sell Last Available Ingredient
- Setup: Product A uses 1 unit of Ingredient X. Ingredient X current stock is 1. Two cashier sessions are active on registered terminals.
- Action: Finalize one Product A order from each terminal at the same time.
- Expected Result: Exactly one order completes, one fails with insufficient stock or concurrency failure. Ingredient X never goes below 0. Exactly one negative sale InventoryTransaction exists.
- Checklist: [ ] Pass [ ] Fail

### Two Products Share One Ingredient
- Setup: Product A and Product B both use 1 unit of Ingredient X. Ingredient X current stock is 2.
- Action: Finalize one Product A order and one Product B order at the same time.
- Expected Result: Both complete. Ingredient X stock becomes 0. Two negative sale InventoryTransactions exist and sum to -2.
- Checklist: [ ] Pass [ ] Fail

### Sale While Stock Adjustment Touches Same Ingredient
- Setup: Product A uses 1 unit of Ingredient X. Ingredient X current stock is 1. A cashier and StockManager both have active sessions.
- Action: Finalize Product A while StockManager adjusts Ingredient X by -1 at the same time.
- Expected Result: Exactly one operation succeeds. Stock remains 0 or greater. Ledger has only the successful movement.
- Checklist: [ ] Pass [ ] Fail

### Failed Finalization Rolls Back
- Setup: Product A requires 2 units of Ingredient X. Ingredient X current stock is 1.
- Action: Attempt to finalize Product A.
- Expected Result: No completed order, no order items, no sale InventoryTransaction, and stock remains 1.
- Checklist: [ ] Pass [ ] Fail

## Order Finalization

### Draft Does Not Deduct Inventory
- Setup: Product A has valid recipe and stock.
- Action: Save Product A as draft.
- Expected Result: Draft order exists, stock unchanged, no sale InventoryTransaction.
- Checklist: [ ] Pass [ ] Fail

### Completed Order Deducts Once
- Setup: Product A uses 1 unit of Ingredient X. Stock is 2.
- Action: Finalize Product A once, then try finalizing the same completed order again.
- Expected Result: First finalization deducts 1. Second attempt fails. Only one sale transaction exists.
- Checklist: [ ] Pass [ ] Fail

### Cancelled Draft Cannot Finalize
- Setup: Create a draft order.
- Action: Cancel the draft, then attempt finalization.
- Expected Result: Finalization fails and inventory remains unchanged.
- Checklist: [ ] Pass [ ] Fail

### Missing Recipe Fails
- Setup: Product A has no ProductIngredient rows.
- Action: Attempt finalization.
- Expected Result: Order fails before commit; no inventory transaction is created.
- Checklist: [ ] Pass [ ] Fail

### Invalid Or Inactive Product Fails
- Setup: Product A is inactive, and Product ID 999 does not exist.
- Action: Attempt finalization for each.
- Expected Result: Both fail before commit.
- Checklist: [ ] Pass [ ] Fail

### Server Recalculates Price
- Setup: Product A price is 25 and uses valid stock.
- Action: Submit an order with manipulated client totals.
- Expected Result: Stored subtotal and total are calculated from Product.Price and server discount rules.
- Checklist: [ ] Pass [ ] Fail

### Receipt Only After Commit
- Setup: Valid product, active session, registered terminal.
- Action: Finalize order, then open receipt.
- Expected Result: Receipt exists only for committed completed order.
- Checklist: [ ] Pass [ ] Fail

## Session Rules

### Cashier Requires Active Session
- Setup: Cashier user has no active session.
- Action: Create draft or finalize order.
- Expected Result: Action fails and redirects or returns validation error.
- Checklist: [ ] Pass [ ] Fail

### StockManager Requires Active Session
- Setup: StockManager user has no active session.
- Action: Add purchase or adjust stock.
- Expected Result: Action fails; no purchase or inventory transaction is created.
- Checklist: [ ] Pass [ ] Fail

### Duplicate Active Session
- Setup: User already has an active session.
- Action: Start another session for same user.
- Expected Result: Duplicate active session is prevented.
- Checklist: [ ] Pass [ ] Fail

### Session Cannot End With Drafts
- Setup: Cashier has at least one draft order.
- Action: End cashier session.
- Expected Result: End session fails until drafts are completed or cancelled.
- Checklist: [ ] Pass [ ] Fail

### Interrupted Session Resumes
- Setup: Mark active session as interrupted.
- Action: Continue the interrupted session.
- Expected Result: Session status returns to Active and heartbeat updates.
- Checklist: [ ] Pass [ ] Fail

## Branch Safety

### Branch A Cannot Use Branch B Product
- Setup: Product B belongs to Branch B. Branch A cashier has active Branch A session.
- Action: Branch A cashier attempts finalization with Product B.
- Expected Result: Finalization fails.
- Checklist: [ ] Pass [ ] Fail

### Branch A Cannot Finalize Branch B Draft
- Setup: Branch B cashier creates a draft.
- Action: Branch A cashier attempts to finalize that draft.
- Expected Result: Finalization fails and Branch B draft remains protected.
- Checklist: [ ] Pass [ ] Fail

### Customer Phone Scoped By Branch
- Setup: Create customer phone 555 in Branch A and Branch B.
- Action: Save both.
- Expected Result: Both are allowed; duplicate in same branch is not allowed.
- Checklist: [ ] Pass [ ] Fail

### Order Number Scoped By Branch
- Setup: Same OrderNumber exists in Branch A and Branch B.
- Action: Save both orders.
- Expected Result: Both are allowed; duplicate in same branch is not allowed.
- Checklist: [ ] Pass [ ] Fail

## Terminal Safety

### Finalize Requires Terminal Identity
- Setup: Cashier has active session.
- Action: Attempt finalization without TerminalId or TerminalCode.
- Expected Result: Finalization fails.
- Checklist: [ ] Pass [ ] Fail

### Inactive Terminal Fails
- Setup: Session points to inactive or unregistered terminal.
- Action: Attempt finalization.
- Expected Result: Finalization fails.
- Checklist: [ ] Pass [ ] Fail

### Order Stores Terminal
- Setup: Valid terminal and active cashier session.
- Action: Finalize order.
- Expected Result: Order stores TerminalId and TerminalCode.
- Checklist: [ ] Pass [ ] Fail

### Terminal Heartbeat Updates
- Setup: Registered active terminal.
- Action: Send heartbeat.
- Expected Result: LastSeenAt updates with current user/session.
- Checklist: [ ] Pass [ ] Fail

## Inventory Ledger

### Current Quantity Equals Ledger
- Setup: Ingredient starts at known quantity.
- Action: Add purchase, sale, and adjustment.
- Expected Result: CurrentQuantity equals starting quantity plus sum of InventoryTransaction.QuantityChanged.
- Checklist: [ ] Pass [ ] Fail

### Purchase Creates Positive Transaction
- Setup: Valid StockManager session.
- Action: Create purchase.
- Expected Result: Purchase transaction is positive and references purchase.
- Checklist: [ ] Pass [ ] Fail

### Sale Creates Negative Transaction
- Setup: Valid cashier session.
- Action: Finalize sale.
- Expected Result: Sale transaction is negative and references order.
- Checklist: [ ] Pass [ ] Fail

### Failed Operations Do Not Log
- Setup: Invalid purchase and insufficient-stock order.
- Action: Attempt both.
- Expected Result: No purchase/order InventoryTransaction is created.
- Checklist: [ ] Pass [ ] Fail

## Validation And Authorization

### Bad Quantities Fail
- Setup: Valid product and stock.
- Action: Submit quantity 0, negative quantity, and huge quantity.
- Expected Result: Each fails safely before commit.
- Checklist: [ ] Pass [ ] Fail

### Delivery Requires Contact Details
- Setup: Valid product and active cashier session.
- Action: Submit delivery order without phone or address.
- Expected Result: Finalization fails.
- Checklist: [ ] Pass [ ] Fail

### Cashier Cannot Manage Stock
- Setup: Cashier has active session.
- Action: Try purchase or adjustment.
- Expected Result: Action is forbidden or service rejects it.
- Checklist: [ ] Pass [ ] Fail

### StockManager Cannot Finalize Sales
- Setup: StockManager has active session.
- Action: Try order finalization.
- Expected Result: Finalization fails unless role policy explicitly allows it.
- Checklist: [ ] Pass [ ] Fail
