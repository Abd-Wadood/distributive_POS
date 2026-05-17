using BranchPOS.Models;

namespace BranchPOS.Services;

public sealed record IdempotencyStartResult(
    bool IsOwner,
    IdempotencyRecord Record,
    string? ErrorMessage);
