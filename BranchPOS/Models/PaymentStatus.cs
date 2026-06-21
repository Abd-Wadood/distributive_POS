namespace BranchPOS.Models;

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    CODPending = 2,
    PartiallyPaid = 3,
    Refunded = 4,
    Cancelled = 5
}
