using System.Security.Claims;

namespace BranchPOS.Services;

public class RequestIdentityService : IRequestIdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITerminalContextService _terminalContextService;

    public RequestIdentityService(IHttpContextAccessor httpContextAccessor, ITerminalContextService terminalContextService)
    {
        _httpContextAccessor = httpContextAccessor;
        _terminalContextService = terminalContextService;
    }

    public async Task<RequestIdentitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var terminal = await _terminalContextService.GetCurrentTerminalAsync(cancellationToken);
        var httpContext = _httpContextAccessor.HttpContext;
        return new RequestIdentitySnapshot(
            GetClientIp(),
            GetUserAgent(),
            httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            terminal?.BranchId,
            terminal?.Id,
            terminal?.TerminalCode);
    }

    public string GetClientIp()
    {
        var context = _httpContextAccessor.HttpContext;
        var forwardedFor = context?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context?.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
    }

    public string GetUserAgent() =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? string.Empty;

    public string NormalizeAttemptedUserName(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}
