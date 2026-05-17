# Idempotency Protection Test Checklist

Use these checks after applying `DbBackedIdempotencyProtection`.

## Order finalization

- Submit the same draft order twice with the same `IdempotencyKey`; only one `Order` is created.
- Double-click the finalize button; only one order, one payment if included, and one stock deduction occur.
- Refresh the receipt page after finalize; the browser stays on the GET receipt flow and does not repeat the POST.
- Send two concurrent `/orders/finalize` requests with the same key; one wins and the other returns the existing result or a friendly in-progress message.
- Reuse the same key with changed order items or totals; the request is rejected with the friendly different-operation message.

## Payments

- Confirm the same payment twice with the same key; only one payment row exists.
- Confirm payment again for an already-paid order; no duplicate cash/card record is created.

## Sessions

- Submit `/sessions/start` twice with the same key; the existing active session is returned.
- Try starting a second active session for the same user; creation is blocked and the active session can be continued.
- Try starting a second active session for the same terminal; creation is blocked.
- Submit `/sessions/close` twice with the same key; the existing closing summary is shown and cash summary is not duplicated.

## Purchases

- Submit the same purchase form twice with the same key; stock increases once only.
- Submit the same supplier invoice number for the same supplier twice; the second request is rejected by uniqueness.
- Reuse a purchase key with different items or prices; the request is rejected.

## Inventory adjustments

- Submit the same manual adjustment twice with the same key; stock changes once only.
- Verify only one `InventoryTransaction` exists for the same adjustment idempotency key.

## Refunds and voids

- When refund/void endpoints are added, submit the same request twice with the same key; stock and cash reverse once only.
- Reuse a refund/void key for a different original order; the request is rejected.

## Durability

- Restart the branch server after a successful state-changing request, then resend the same key; the existing result is returned.
- Verify completed idempotency records remain until retention cleanup and are not stored only in memory.

## Audit

- Confirm audit logs are written for duplicate request detection.
- Confirm audit logs are written when a key is reused with different request data.
- Confirm audit logs are written when a completed duplicate returns an existing resource.
