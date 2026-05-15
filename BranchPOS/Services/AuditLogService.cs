using System.Security.Claims;
using System.Text.Json;
using BranchPOS.Data;
using BranchPOS.Models;

namespace BranchPOS.Services;

public class AuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        object? oldValues = null,
        object? newValues = null,
        int? branchId = null,
        int? terminalId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        userId ??= httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        _context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, SerializerOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, SerializerOptions),
            BranchId = branchId,
            TerminalId = terminalId,
            UserId = userId,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
