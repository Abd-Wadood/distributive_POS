using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BranchPOS.Services;

public class TerminalContextService : ITerminalContextService
{
    public const string TerminalCodeCookieName = "BranchPOS.TerminalCode";
    public const string TerminalIdentityCookieName = "BranchPOS.Terminal";

    private const int TokenByteLength = 32;

    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtector _protector;
    private readonly PosOperationalOptions _options;
    private readonly IWebHostEnvironment _environment;

    public TerminalContextService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PosOperationalOptions> options,
        IWebHostEnvironment environment)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("BranchPOS.TerminalIdentity.v1");
        _options = options.Value;
        _environment = environment;
    }

    public string? GetTerminalCodeFromRequest()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var identity = ReadIdentityFromRequest(httpContext);
        if (identity is not null)
        {
            return identity.TerminalCode;
        }

        return httpContext.Request.Cookies.TryGetValue(TerminalCodeCookieName, out var code)
            ? NormalizeCode(code)
            : null;
    }

    public async Task<Terminal?> GetCurrentTerminalAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var identity = ReadIdentityFromRequest(httpContext);
        var terminalCode = identity?.TerminalCode;
        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            if (!httpContext.Request.Cookies.TryGetValue(TerminalCodeCookieName, out var legacyCode))
            {
                return null;
            }

            terminalCode = NormalizeCode(legacyCode);
        }

        var terminal = await _context.Terminals
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.TerminalCode == terminalCode && x.IsActive, cancellationToken);

        if (terminal is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(terminal.TerminalTokenHash))
        {
            return identity is not null && VerifyTerminalToken(identity.Token, terminal.TerminalTokenHash)
                ? terminal
                : null;
        }

        return identity is null || string.IsNullOrWhiteSpace(identity.Token)
            ? terminal
            : terminal;
    }

    public async Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
        await GetCurrentTerminalAsync(cancellationToken)
        ?? throw new BusinessException("Terminal identity is missing, invalid, or inactive. Register this terminal before continuing.");

    public async Task IssueTerminalCookieAsync(Terminal terminal, string? rawToken = null, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new BusinessException("Terminal cookie cannot be issued outside an HTTP request.");

        rawToken ??= GenerateTerminalToken();
        terminal.TerminalCode = NormalizeCode(terminal.TerminalCode);
        terminal.TerminalTokenHash = HashTerminalToken(rawToken);
        await _context.SaveChangesAsync(cancellationToken);

        var payload = _protector.Protect(JsonSerializer.Serialize(new TerminalIdentity(terminal.TerminalCode, rawToken)));
        httpContext.Response.Cookies.Append(TerminalIdentityCookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = _environment.IsProduction() && _options.RequireSecureTerminalCookieInProduction,
            Expires = DateTimeOffset.UtcNow.Add(_options.TerminalCookieExpiration)
        });

        httpContext.Response.Cookies.Delete(TerminalCodeCookieName);
    }

    public async Task HeartbeatAsync(string? userId = null, int? sessionId = null, CancellationToken cancellationToken = default)
    {
        var terminal = await GetCurrentTerminalAsync(cancellationToken);
        if (terminal is null)
        {
            return;
        }

        var heartbeat = await _context.TerminalHeartbeats.FirstOrDefaultAsync(x => x.TerminalId == terminal.Id, cancellationToken);
        var now = DateTime.UtcNow;
        if (heartbeat is not null && heartbeat.LastSeenAt > now - _options.HeartbeatWriteInterval)
        {
            return;
        }

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

        heartbeat.LastSeenAt = now;
        heartbeat.CurrentUserId = userId;
        heartbeat.CurrentSessionId = sessionId;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            _context.ChangeTracker.Clear();
        }
    }

    public bool IsOffline(TerminalHeartbeat heartbeat, DateTime? now = null) =>
        heartbeat.LastSeenAt < (now ?? DateTime.UtcNow) - _options.TerminalOfflineThreshold;

    public static string NormalizeCode(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string GenerateTerminalToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenByteLength));

    public static string HashTerminalToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public static bool VerifyTerminalToken(string token, string expectedHash)
    {
        var hash = HashTerminalToken(token);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hash),
            Encoding.ASCII.GetBytes(expectedHash));
    }

    private TerminalIdentity? ReadIdentityFromRequest(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(TerminalIdentityCookieName, out var protectedPayload) ||
            string.IsNullOrWhiteSpace(protectedPayload))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(protectedPayload);
            var identity = JsonSerializer.Deserialize<TerminalIdentity>(payload);
            if (identity is null || string.IsNullOrWhiteSpace(identity.TerminalCode) || string.IsNullOrWhiteSpace(identity.Token))
            {
                return null;
            }

            return identity with { TerminalCode = NormalizeCode(identity.TerminalCode) };
        }
        catch
        {
            return null;
        }
    }

    private sealed record TerminalIdentity(string TerminalCode, string Token);
}
