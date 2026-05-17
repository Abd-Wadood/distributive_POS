namespace BranchPOS.Services;

public class IdempotencyOptions
{
    public int RetentionDays { get; set; } = 14;
}
