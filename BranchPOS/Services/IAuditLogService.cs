namespace BranchPOS.Services;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        int? branchId = null,
        int? terminalId = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
