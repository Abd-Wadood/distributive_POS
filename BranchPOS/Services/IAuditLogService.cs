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

    Task LogSecurityAsync(
        string eventType,
        string severity,
        string message,
        string? userId = null,
        string? attemptedUserName = null,
        int? branchId = null,
        int? terminalId = null,
        CancellationToken cancellationToken = default);
}
