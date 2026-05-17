namespace BranchPOS.Services;

public sealed record RequestIdentitySnapshot(
    string ClientIp,
    string UserAgent,
    string? UserId,
    int? BranchId,
    int? TerminalId,
    string? TerminalCode);
