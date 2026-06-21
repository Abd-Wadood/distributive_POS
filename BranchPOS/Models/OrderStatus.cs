namespace BranchPOS.Models;

public enum OrderStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Cancelled = 4,
    Refunded = 5,
    UnknownFinalize = 6,
    ReceiptFailed = 7,
    CancelledAfterPreparation = 8,
    SentToKitchen = 9,
    Preparing = 10,
    Ready = 11,
    Dispatched = 12,
    CancelledAsWaste = 13
}
