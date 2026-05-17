namespace BranchPOS.Models;

public enum IdempotencyStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3
}
