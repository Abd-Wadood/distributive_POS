using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BranchPOS.Services;

public class LoginSecurityService : ILoginSecurityService
{
    private readonly IMemoryCache _cache;
    private readonly IRequestIdentityService _requestIdentity;
    private readonly IAuditLogService _auditLogService;
    private readonly SecurityRateLimitOptions _options;

    public LoginSecurityService(
        IMemoryCache cache,
        IRequestIdentityService requestIdentity,
        IAuditLogService auditLogService,
        IOptions<SecurityRateLimitOptions> options)
    {
        _cache = cache;
        _requestIdentity = requestIdentity;
        _auditLogService = auditLogService;
        _options = options.Value;
    }

    public async Task<bool> IsBlockedAsync(string attemptedUserName, CancellationToken cancellationToken = default)
    {
        var normalized = _requestIdentity.NormalizeAttemptedUserName(attemptedUserName);
        var ip = _requestIdentity.GetClientIp();
        var userCount = _cache.Get<int>(UserFailureKey(ip, normalized));
        var ipCount = _cache.Get<int>(IpFailureKey(ip));
        var blocked = userCount >= _options.LoginFailedPermitLimit || ipCount >= _options.LoginIpFailedPermitLimit;
        if (blocked)
        {
            await _auditLogService.LogSecurityAsync("LoginRateLimitHit", "Warning",
                "Too many login attempts from this IP or username.",
                attemptedUserName: normalized,
                cancellationToken: cancellationToken);
        }

        return blocked;
    }

    public async Task RecordFailureAsync(string attemptedUserName, string? userId, CancellationToken cancellationToken = default)
    {
        var normalized = _requestIdentity.NormalizeAttemptedUserName(attemptedUserName);
        var ip = _requestIdentity.GetClientIp();
        Increment(UserFailureKey(ip, normalized));
        var ipFailures = Increment(IpFailureKey(ip));
        await TrackUserRotationAsync(ip, normalized, cancellationToken);

        await _auditLogService.LogSecurityAsync("LoginFailed", "Warning",
            "Login failed.",
            userId: userId,
            attemptedUserName: normalized,
            cancellationToken: cancellationToken);

        if (ipFailures >= _options.LoginIpFailedPermitLimit)
        {
            await _auditLogService.LogSecurityAsync("RepeatedLoginFailuresFromIp", "Critical",
                "Repeated login failures detected from one IP.",
                attemptedUserName: normalized,
                cancellationToken: cancellationToken);
        }
    }

    public async Task RecordSuccessAsync(string attemptedUserName, string? userId, CancellationToken cancellationToken = default)
    {
        var normalized = _requestIdentity.NormalizeAttemptedUserName(attemptedUserName);
        var ip = _requestIdentity.GetClientIp();
        if (_cache.Get<int>(UserFailureKey(ip, normalized)) > 0 || _cache.Get<int>(IpFailureKey(ip)) > 0)
        {
            await _auditLogService.LogSecurityAsync("LoginSucceededAfterFailures", "Info",
                "Login succeeded after previous failed attempts.",
                userId: userId,
                attemptedUserName: normalized,
                cancellationToken: cancellationToken);
        }
    }

    private int Increment(string key)
    {
        var count = _cache.Get<int>(key) + 1;
        _cache.Set(key, count, TimeSpan.FromMinutes(1));
        return count;
    }

    private async Task TrackUserRotationAsync(string ip, string normalizedUserName, CancellationToken cancellationToken)
    {
        var key = $"login:rotation:{ip}";
        var users = _cache.Get<HashSet<string>>(key) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        users.Add(normalizedUserName);
        _cache.Set(key, users, TimeSpan.FromMinutes(5));
        if (users.Count >= 5)
        {
            await _auditLogService.LogSecurityAsync("SuspiciousUsernameRotation", "Critical",
                "Multiple usernames were attempted from the same IP.",
                attemptedUserName: normalizedUserName,
                cancellationToken: cancellationToken);
        }
    }

    private static string UserFailureKey(string ip, string normalizedUserName) =>
        $"login:fail:user:{ip}:{normalizedUserName}";

    private static string IpFailureKey(string ip) =>
        $"login:fail:ip:{ip}";
}
