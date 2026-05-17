namespace BranchPOS.Services;

public interface ILoginSecurityService
{
    Task<bool> IsBlockedAsync(string attemptedUserName, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string attemptedUserName, string? userId, CancellationToken cancellationToken = default);

    Task RecordSuccessAsync(string attemptedUserName, string? userId, CancellationToken cancellationToken = default);
}
