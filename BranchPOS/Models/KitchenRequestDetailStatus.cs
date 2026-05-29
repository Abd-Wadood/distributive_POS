namespace BranchPOS.Models;

public enum KitchenRequestDetailStatus
{
    PendingManagerReview,
    Approved,
    Rejected,
    Dispatched,
    PartiallyDispatched,
    Received,
    Cancelled
}
