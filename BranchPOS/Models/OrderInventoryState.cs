namespace BranchPOS.Models;

public enum OrderInventoryState
{
    None = 0,
    Reserved = 1,
    Released = 2,
    Consumed = 3,
    Wasted = 4,
    Restored = 5
}
