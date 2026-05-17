namespace BranchPOS.Services;

public interface IRequestIdentityService
{
    Task<RequestIdentitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    string GetClientIp();

    string GetUserAgent();

    string NormalizeAttemptedUserName(string? value);
}
