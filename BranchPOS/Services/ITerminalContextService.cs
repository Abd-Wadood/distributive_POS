using BranchPOS.Models;

namespace BranchPOS.Services;

public interface ITerminalContextService
{
    string? GetTerminalCodeFromRequest();

    Task<Terminal?> GetCurrentTerminalAsync(CancellationToken cancellationToken = default);

    Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default);

    Task IssueTerminalCookieAsync(Terminal terminal, string? rawToken = null, CancellationToken cancellationToken = default);

    Task HeartbeatAsync(string? userId = null, int? sessionId = null, CancellationToken cancellationToken = default);
}
