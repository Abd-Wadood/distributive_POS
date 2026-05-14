using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class TerminalContextService : ITerminalContextService
{
    public const string TerminalCodeCookieName = "BranchPOS.TerminalCode";

    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TerminalContextService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetTerminalCodeFromRequest()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        return httpContext.Request.Cookies.TryGetValue(TerminalCodeCookieName, out var code)
            ? NormalizeCode(code)
            : null;
    }

    public async Task<Terminal?> GetCurrentTerminalAsync(CancellationToken cancellationToken = default)
    {
        var terminalCode = GetTerminalCodeFromRequest();
        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            return null;
        }

        return await _context.Terminals
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.TerminalCode == terminalCode && x.IsActive, cancellationToken);
    }

    public async Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
        await GetCurrentTerminalAsync(cancellationToken)
        ?? throw new BusinessException("Terminal is not registered or is inactive. Register this terminal before continuing.");

    public async Task HeartbeatAsync(string? userId = null, int? sessionId = null, CancellationToken cancellationToken = default)
    {
        var terminal = await GetCurrentTerminalAsync(cancellationToken);
        if (terminal is null)
        {
            return;
        }

        var heartbeat = await _context.TerminalHeartbeats.FirstOrDefaultAsync(x => x.TerminalId == terminal.Id, cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new TerminalHeartbeat
            {
                TerminalId = terminal.Id,
                TerminalCode = terminal.TerminalCode,
                BranchId = terminal.BranchId
            };
            _context.TerminalHeartbeats.Add(heartbeat);
        }

        heartbeat.LastSeenAt = DateTime.UtcNow;
        heartbeat.CurrentUserId = userId;
        heartbeat.CurrentSessionId = sessionId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();
}
