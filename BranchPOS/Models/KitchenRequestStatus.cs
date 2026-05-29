namespace BranchPOS.Models;

public enum KitchenRequestStatus
{
    Pending,
    PendingManagerReview,
    Approved,
    Rejected,
    Dispatched,
    PartiallyDispatched,
    Received,
    Cancelled
}
