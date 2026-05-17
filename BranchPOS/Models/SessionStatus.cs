namespace BranchPOS.Models;

public enum SessionStatus
{
    Active = 1,
    ClosingPending = 2,
    Closed = 3,
    Reopened = 4,
    ForceClosed = 5,
    Abandoned = 6
}
