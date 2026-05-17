using BranchPOS.Models;

namespace BranchPOS.Services;

public interface IIdempotencyService
{
    string GetOrCreateKey();

    string HashPayload(object payload);

    Task<IdempotencyStartResult> BeginAsync(
        string operationType,
        string idempotencyKey,
        string requestHash,
        string? userId,
        int? branchId,
        int? terminalId,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IdempotencyRecord record,
        string resourceType,
        int resourceId,
        int responseCode,
        string responseBodySummary,
        CancellationToken cancellationToken = default);

    Task FailAsync(IdempotencyRecord record, string message, CancellationToken cancellationToken = default);
}
