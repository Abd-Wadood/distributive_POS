using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BranchPOS.Services;

public class IdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditLogService _auditLogService;
    private readonly IdempotencyOptions _options;

    public IdempotencyService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuditLogService auditLogService,
        IOptions<IdempotencyOptions> options)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _auditLogService = auditLogService;
        _options = options.Value;
    }

    public string GetOrCreateKey()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers["Idempotency-Key"].FirstOrDefault();
        return IsValidKey(header) ? header! : Guid.NewGuid().ToString("N");
    }

    public string HashPayload(object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public async Task<IdempotencyStartResult> BeginAsync(
        string operationType,
        string idempotencyKey,
        string requestHash,
        string? userId,
        int? branchId,
        int? terminalId,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidKey(idempotencyKey))
        {
            throw new PosValidationException("Invalid request key. Please refresh and try again.");
        }

        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [$"idempotency:{idempotencyKey}"], cancellationToken);

        var existing = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (!string.Equals(existing.OperationType, operationType, StringComparison.Ordinal) ||
                !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                await _auditLogService.LogSecurityAsync("IdempotencyKeyReusedWithDifferentRequest", "Critical",
                    "An idempotency key was reused with different request data.",
                    userId: userId,
                    branchId: branchId,
                    terminalId: terminalId,
                    cancellationToken: cancellationToken);
                return new IdempotencyStartResult(false, existing, "This request key was already used for a different operation. Please refresh and try again.");
            }

            var eventType = existing.Status == IdempotencyStatus.Completed
                ? "DuplicateRequestReturnedExistingResult"
                : "DuplicateRequestDetected";
            await _auditLogService.LogSecurityAsync(eventType, "Info",
                $"Duplicate {operationType} request detected.",
                userId: userId,
                branchId: branchId,
                terminalId: terminalId,
                cancellationToken: cancellationToken);

            if (existing.Status == IdempotencyStatus.InProgress)
            {
                return new IdempotencyStartResult(false, existing, "This request is already being processed. Please wait.");
            }

            return new IdempotencyStartResult(false, existing, null);
        }

        var record = new IdempotencyRecord
        {
            IdempotencyKey = idempotencyKey,
            OperationType = operationType,
            RequestHash = requestHash,
            UserId = userId,
            BranchId = branchId,
            TerminalId = terminalId,
            ReferenceType = operationType,
            Status = IdempotencyStatus.InProgress,
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, _options.RetentionDays))
        };

        _context.IdempotencyRecords.Add(record);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            _context.ChangeTracker.Clear();
            var concurrent = await _context.IdempotencyRecords
                .FirstAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            await _auditLogService.LogSecurityAsync("DuplicateRequestDetected", "Info",
                $"Concurrent duplicate {operationType} request detected.",
                userId: userId,
                branchId: branchId,
                terminalId: terminalId,
                cancellationToken: cancellationToken);
            if (!string.Equals(concurrent.OperationType, operationType, StringComparison.Ordinal) ||
                !string.Equals(concurrent.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyStartResult(false, concurrent, "This request key was already used for a different operation. Please refresh and try again.");
            }

            return new IdempotencyStartResult(false, concurrent,
                concurrent.Status == IdempotencyStatus.InProgress ? "This request is already being processed. Please wait." : null);
        }
        await _auditLogService.LogSecurityAsync("IdempotencyKeyCreated", "Info",
            $"Idempotency key created for {operationType}.",
            userId: userId,
            branchId: branchId,
            terminalId: terminalId,
            cancellationToken: cancellationToken);
        return new IdempotencyStartResult(true, record, null);
    }

    public async Task CompleteAsync(
        IdempotencyRecord record,
        string resourceType,
        int resourceId,
        int responseCode,
        string responseBodySummary,
        CancellationToken cancellationToken = default)
    {
        record.Status = IdempotencyStatus.Completed;
        record.ResourceType = resourceType;
        record.ResourceId = resourceId;
        record.ReferenceType = resourceType;
        record.ReferenceId = resourceId;
        record.ResponseCode = responseCode;
        record.ResponseBodySummary = responseBodySummary.Length > 500 ? responseBodySummary[..500] : responseBodySummary;
        record.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(IdempotencyRecord record, string message, CancellationToken cancellationToken = default)
    {
        record.Status = IdempotencyStatus.Failed;
        record.ResponseBodySummary = message.Length > 500 ? message[..500] : message;
        record.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.Length <= 120 &&
        key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}
