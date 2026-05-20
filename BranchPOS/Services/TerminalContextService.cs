using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan TerminalLookupCacheTtl = TimeSpan.FromMinutes(3);

    public TerminalContextService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PosOperationalOptions> options,
        IWebHostEnvironment environment,
        IMemoryCache cache)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _protector = dataProtectionProvider.CreateProtector("BranchPOS.TerminalIdentity.v1");
        _options = options.Value;
        _environment = environment;
        _cache = cache;
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
        var terminalCode = GetTerminalCodeForLookup(out var identity);
        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            return null;
        }

        var cacheKey = GetTerminalCacheKey(terminalCode);
        if (!_cache.TryGetValue(cacheKey, out CachedTerminal? cached) || cached is null)
        {
            var terminal = await LoadTerminalFromDatabaseAsync(terminalCode, cancellationToken);
            if (terminal is null)
            {
                return null;
            }

            cached = CachedTerminal.FromTerminal(terminal);
            _cache.Set(cacheKey, cached, TerminalLookupCacheTtl);
        }

        return ToTerminalIfIdentityIsValid(cached, identity);
    }

    public async Task<Terminal?> GetCurrentTerminalFreshAsync(CancellationToken cancellationToken = default)
    {
        var terminalCode = GetTerminalCodeForLookup(out var identity);
        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            return null;
        }

        var terminal = await LoadTerminalFromDatabaseAsync(terminalCode, cancellationToken);
        if (terminal is null)
        {
            return null;
        }

        var cached = CachedTerminal.FromTerminal(terminal);
        _cache.Set(GetTerminalCacheKey(terminalCode), cached, TerminalLookupCacheTtl);
        return ToTerminalIfIdentityIsValid(cached, identity);
    }

    private string? GetTerminalCodeForLookup(out TerminalIdentity? identity)
    {
        identity = null;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        identity = ReadIdentityFromRequest(httpContext);
        var terminalCode = identity?.TerminalCode;
        if (string.IsNullOrWhiteSpace(terminalCode))
        {
            if (!httpContext.Request.Cookies.TryGetValue(TerminalCodeCookieName, out var legacyCode))
            {
                return null;
            }

            terminalCode = NormalizeCode(legacyCode);
        }

        return terminalCode;
    }

    private async Task<Terminal?> LoadTerminalFromDatabaseAsync(string terminalCode, CancellationToken cancellationToken) =>
        await _context.Terminals
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.TerminalCode == terminalCode && x.IsActive, cancellationToken);

    private Terminal? ToTerminalIfIdentityIsValid(CachedTerminal cached, TerminalIdentity? identity)
    {
        if (!cached.IsActive)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(cached.TerminalTokenHash))
        {
            return identity is not null && VerifyTerminalToken(identity.Token, cached.TerminalTokenHash)
                ? cached.ToTerminal()
                : null;
        }

        return identity is null || string.IsNullOrWhiteSpace(identity.Token)
            ? cached.ToTerminal()
            : cached.ToTerminal();
    }

    public async Task<Terminal> RequireCurrentTerminalAsync(CancellationToken cancellationToken = default) =>
        await GetCurrentTerminalAsync(cancellationToken)
        ?? throw new BusinessException("Terminal identity is missing, invalid, or inactive. Register this terminal before continuing.");

    public async Task<Terminal> RequireCurrentTerminalFreshAsync(CancellationToken cancellationToken = default) =>
        await GetCurrentTerminalFreshAsync(cancellationToken)
        ?? throw new BusinessException("Terminal identity is missing, invalid, or inactive. Register this terminal before continuing.");

    public async Task IssueTerminalCookieAsync(Terminal terminal, string? rawToken = null, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new BusinessException("Terminal cookie cannot be issued outside an HTTP request.");

        rawToken ??= GenerateTerminalToken();
        terminal.TerminalCode = NormalizeCode(terminal.TerminalCode);
        terminal.TerminalTokenHash = HashTerminalToken(rawToken);
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(GetTerminalCacheKey(terminal.TerminalCode));

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

    public static string GetTerminalCacheKey(string terminalCode) =>
        $"terminal:v1:{NormalizeCode(terminalCode)}";

    private sealed record CachedTerminal(
        int Id,
        int BranchId,
        string TerminalCode,
        string Name,
        string? IpAddress,
        bool IsActive,
        string? TerminalTokenHash,
        string? BranchName)
    {
        public static CachedTerminal FromTerminal(Terminal terminal) =>
            new(
                terminal.Id,
                terminal.BranchId,
                terminal.TerminalCode,
                terminal.Name,
                terminal.IpAddress,
                terminal.IsActive,
                terminal.TerminalTokenHash,
                terminal.Branch?.Name);

        public Terminal ToTerminal() =>
            new()
            {
                Id = Id,
                BranchId = BranchId,
                TerminalCode = TerminalCode,
                Name = Name,
                IpAddress = IpAddress,
                IsActive = IsActive,
                TerminalTokenHash = TerminalTokenHash,
                Branch = string.IsNullOrWhiteSpace(BranchName)
                    ? null
                    : new Branch { Id = BranchId, Name = BranchName }
            };
    }
}
