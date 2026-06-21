using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BranchPOS.Services;

public class UserSessionService : IUserSessionService
{
    private readonly AppDbContext _context;
    private readonly IBranchService _branchService;
    private readonly ITerminalContextService _terminalContextService;
    private readonly ISessionCodeGeneratorService _sessionCodeGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly PosOperationalOptions _options;
    private readonly IMemoryCache _cache;
    private static readonly SessionStatus[] OperationalStatuses = [SessionStatus.Active, SessionStatus.Reopened];
    private static readonly SessionStatus[] BlockingStatuses = [SessionStatus.Active, SessionStatus.Reopened, SessionStatus.ClosingPending];
    private static readonly TimeSpan ActiveSessionCacheTtl = TimeSpan.FromSeconds(45);

    public UserSessionService(
        AppDbContext context,
        IBranchService branchService,
        ITerminalContextService terminalContextService,
        ISessionCodeGeneratorService sessionCodeGenerator,
        IAuditLogService auditLogService,
        IIdempotencyService idempotencyService,
        IOptions<PosOperationalOptions> options,
        IMemoryCache cache)
    {
        _context = context;
        _branchService = branchService;
        _terminalContextService = terminalContextService;
        _sessionCodeGenerator = sessionCodeGenerator;
        _auditLogService = auditLogService;
        _idempotencyService = idempotencyService;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.OpeningCashAmount < 0)
        {
            throw new PosValidationException("Opening cash amount cannot be negative.");
        }

        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal is not registered. Register this terminal before starting a session.");
        }

        await _branchService.EnsureBranchAccessAsync(dto.UserId, dto.BranchId, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [dto.UserId], cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [$"terminal:{dto.TerminalId}"], cancellationToken);
        var idempotencyHash = _idempotencyService.HashPayload(new
        {
            dto.UserId,
            dto.BranchId,
            dto.RoleName,
            dto.TerminalId,
            dto.TerminalCode,
            dto.OpeningCashAmount
        });
        var idempotency = await _idempotencyService.BeginAsync("SessionStart", dto.IdempotencyKey, idempotencyHash, dto.UserId, dto.BranchId, dto.TerminalId, cancellationToken);
        if (!idempotency.IsOwner)
        {
            if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
            {
                throw new BusinessException(idempotency.ErrorMessage);
            }

            if (idempotency.Record.ResourceId.HasValue)
            {
                var existing = await _context.UserSessions.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == idempotency.Record.ResourceId.Value, cancellationToken)
                    ?? throw new PosNotFoundException("The previous session result was not found. Refresh and try again.");
                await transaction.CommitAsync(cancellationToken);
                InvalidateActiveSessionCache(existing.UserId, existing.TerminalId, existing.BranchId);
                return existing;
            }

            throw new BusinessException("This request is already being processed. Please wait.");
        }

        var existingUserSession = await _context.UserSessions
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.UserId == dto.UserId && BlockingStatuses.Contains(x.Status), cancellationToken);
        if (existingUserSession is not null)
        {
            await _auditLogService.LogAsync("SessionStartBlockedActiveSessionExists", nameof(UserSession), existingUserSession.Id.ToString(), null,
                new { existingUserSession.SessionCode, existingUserSession.Status, existingUserSession.BranchId, existingUserSession.TerminalId },
                existingUserSession.BranchId, existingUserSession.TerminalId, dto.UserId, cancellationToken);
            await _idempotencyService.CompleteAsync(idempotency.Record, nameof(UserSession), existingUserSession.Id, StatusCodes.Status200OK, existingUserSession.SessionCode, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidateActiveSessionCache(existingUserSession.UserId, existingUserSession.TerminalId, existingUserSession.BranchId);
            return existingUserSession;
        }

        var existingTerminalSession = await _context.UserSessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TerminalId == dto.TerminalId && BlockingStatuses.Contains(x.Status), cancellationToken);
        if (existingTerminalSession is not null)
        {
            await _auditLogService.LogAsync("SessionStartBlockedTerminalActive", nameof(UserSession), existingTerminalSession.Id.ToString(), null,
                new { existingTerminalSession.SessionCode, existingTerminalSession.Status, existingTerminalSession.UserId, existingTerminalSession.TerminalId },
                existingTerminalSession.BranchId, existingTerminalSession.TerminalId, dto.UserId, cancellationToken);
            await _idempotencyService.FailAsync(idempotency.Record, "Terminal already has an active session.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new BusinessException($"Terminal {dto.TerminalCode} already has an active session ({existingTerminalSession.SessionCode}). Continue or close that session before starting another.");
        }

        var session = new UserSession
        {
            SessionCode = await _sessionCodeGenerator.GenerateAsync(cancellationToken),
            UserId = dto.UserId,
            BranchId = dto.BranchId,
            RoleName = dto.RoleName,
            TerminalName = string.IsNullOrWhiteSpace(dto.TerminalName) ? Environment.MachineName : dto.TerminalName.Trim(),
            TerminalId = dto.TerminalId,
            TerminalCode = dto.TerminalCode,
            OpeningCashAmount = dto.OpeningCashAmount,
            IdempotencyKey = dto.IdempotencyKey,
            Notes = dto.Notes
        };

        _context.UserSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _idempotencyService.CompleteAsync(idempotency.Record, nameof(UserSession), session.Id, StatusCodes.Status200OK, session.SessionCode, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw DatabaseErrorTranslator.ToUserException(ex, "A session is already active for this user or terminal. Continue or close it before starting another.");
        }

        await HeartbeatAsync(session.Id, session.TerminalName, cancellationToken);
        await _auditLogService.LogAsync("SessionStarted", nameof(UserSession), session.Id.ToString(), null,
            new { session.SessionCode, session.BranchId, session.TerminalId, session.TerminalCode, session.RoleName, session.OpeningCashAmount },
            session.BranchId, session.TerminalId, session.UserId, cancellationToken);
        InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
        return session;
    }

    public async Task<UserSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default)
    {
        var terminal = await _terminalContextService.GetCurrentTerminalAsync(cancellationToken);
        var cacheKey = GetActiveSessionCacheKey(userId, terminal?.Id, terminal?.BranchId);
        if (_cache.TryGetValue(cacheKey, out UserSession? cached))
        {
            return cached is null ? null : CloneSession(cached);
        }

        var session = await GetActiveSessionFromDatabaseAsync(userId, terminal?.Id, terminal?.BranchId, cancellationToken);
        if (session is not null)
        {
            _cache.Set(cacheKey, CloneSession(session), ActiveSessionCacheTtl);
        }

        return session;
    }

    public Task<UserSession?> GetActiveSessionFreshAsync(string userId, CancellationToken cancellationToken = default) =>
        GetActiveSessionFromDatabaseAsync(userId, null, null, cancellationToken);

    private Task<UserSession?> GetActiveSessionFromDatabaseAsync(string userId, int? terminalId, int? branchId, CancellationToken cancellationToken)
    {
        var query = _context.UserSessions
            .Include(x => x.Branch)
            .Where(x => x.UserId == userId && BlockingStatuses.Contains(x.Status))
            .AsQueryable();

        if (terminalId.HasValue)
        {
            query = query.Where(x => x.TerminalId == terminalId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return query.OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserSession?> GetAbandonedSessionAsync(string userId, CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Include(x => x.Branch)
            .Where(x => x.UserId == userId && x.Status == SessionStatus.Abandoned)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserSession?> GetActiveSessionForTerminalAsync(int terminalId, CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Include(x => x.Branch)
            .Include(x => x.User)
            .Where(x => x.TerminalId == terminalId && BlockingStatuses.Contains(x.Status))
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSession> ContinueSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == sessionId &&
            x.UserId == userId &&
            (x.Status == SessionStatus.Active || x.Status == SessionStatus.Reopened || x.Status == SessionStatus.Abandoned), cancellationToken);

        if (session is null)
        {
            throw new PosNotFoundException("Session was not found. Refresh the page and try again.");
        }

        var active = await GetActiveSessionAsync(userId, cancellationToken);
        if (active is not null && active.Id != session.Id)
        {
            throw new BusinessException("End the current active session before resuming another session.");
        }

        await _branchService.EnsureBranchAccessAsync(userId, session.BranchId, cancellationToken);
        var terminal = await _terminalContextService.RequireCurrentTerminalAsync(cancellationToken);
        if (!_options.AllowSessionResumeFromDifferentTerminal && terminal.Id != session.TerminalId)
        {
            throw new BusinessException("Resume this session from the same terminal where it was started.");
        }

        var oldValues = new { session.Status, session.TerminalId, session.TerminalCode, session.TerminalName };
        if (_options.AllowSessionResumeFromDifferentTerminal && terminal.Id != session.TerminalId)
        {
            session.TerminalId = terminal.Id;
            session.TerminalCode = terminal.TerminalCode;
            session.TerminalName = terminal.Name;
        }

        session.Status = session.Status == SessionStatus.Abandoned ? SessionStatus.Reopened : session.Status;
        session.EndedAt = null;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw DatabaseErrorTranslator.ToUserException(ex, "End the current active session before resuming another session.");
        }

        await HeartbeatAsync(session.Id, session.TerminalName, cancellationToken);
        await _auditLogService.LogAsync("SessionContinued", nameof(UserSession), session.Id.ToString(), oldValues,
            new { session.Status, session.TerminalId, session.TerminalCode, session.TerminalName },
            session.BranchId, session.TerminalId, session.UserId, cancellationToken);
        InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
        return session;
    }

    public async Task<SessionCloseViewModel> GetCloseSessionAsync(int sessionId, string userId, bool isManagerOrAdmin, CancellationToken cancellationToken = default)
    {
        var session = await GetClosableSessionAsync(sessionId, userId, isManagerOrAdmin, cancellationToken);
        return await BuildCloseViewModelAsync(session, cancellationToken);
    }

    public async Task<List<PendingSessionCloseApprovalViewModel>> GetPendingCloseApprovalsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UserSessions
            .AsNoTracking()
            .Where(x => x.Status == SessionStatus.ClosingPending && x.RequiresManagerApproval)
            .OrderBy(x => x.ClosingRequestedAt ?? x.StartedAt)
            .Select(x => new PendingSessionCloseApprovalViewModel
            {
                SessionId = x.Id,
                SessionCode = x.SessionCode,
                Cashier = x.User == null ? x.UserId : x.User.Email ?? x.UserId,
                BranchName = x.Branch == null ? "" : x.Branch.Name,
                TerminalName = x.TerminalName,
                TerminalCode = x.TerminalCode,
                StartedAt = x.StartedAt,
                RequestedAt = x.ClosingRequestedAt,
                OpeningCash = x.OpeningCashAmount,
                ExpectedClosingCash = x.ExpectedClosingCash ?? x.OpeningCashAmount + _context.Orders
                    .Where(o => o.UserSessionId == x.Id && (o.PaymentStatus == PaymentStatus.Paid || (o.PaymentStatus == PaymentStatus.Unpaid && o.OrderStatus == OrderStatus.Completed)))
                    .Sum(o => o.TotalAmount),
                CountedClosingCash = x.CountedClosingCash ?? 0,
                CompletedOrdersCount = _context.Orders.Count(o => o.UserSessionId == x.Id && (o.PaymentStatus == PaymentStatus.Paid || (o.PaymentStatus == PaymentStatus.Unpaid && o.OrderStatus == OrderStatus.Completed))),
                TotalSalesAmount = _context.Orders
                    .Where(o => o.UserSessionId == x.Id && (o.PaymentStatus == PaymentStatus.Paid || (o.PaymentStatus == PaymentStatus.Unpaid && o.OrderStatus == OrderStatus.Completed)))
                    .Sum(o => o.TotalAmount),
                IdempotencyKey = Guid.NewGuid().ToString("N")
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserSession> ApprovePendingCloseAsync(int sessionId, string approvedByUserId, int terminalId, string terminalCode, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var pending = await _context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.Status == SessionStatus.ClosingPending && x.RequiresManagerApproval, cancellationToken)
            ?? throw new PosNotFoundException("Pending session close request was not found. Refresh and try again.");

        if (!pending.CountedClosingCash.HasValue)
        {
            throw new BusinessException("This close request has no counted cash amount. Open the session close screen and enter the counted cash.");
        }

        return await CloseSessionAsync(new CloseSessionDto
        {
            SessionId = pending.Id,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? _idempotencyService.GetOrCreateKey() : idempotencyKey,
            UserId = approvedByUserId,
            TerminalId = terminalId,
            TerminalCode = terminalCode,
            CountedClosingCash = pending.CountedClosingCash.Value,
            ConfirmationText = "END",
            IsManagerOrAdmin = true,
            ForceClose = false
        }, cancellationToken);
    }

    public async Task<UserSession> CloseSessionAsync(CloseSessionDto dto, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(dto.ConfirmationText?.Trim(), "END", StringComparison.Ordinal))
        {
            throw new PosValidationException("Type END to confirm session closing.");
        }

        if (dto.CountedClosingCash < 0)
        {
            throw new PosValidationException("Counted closing cash cannot be negative.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var session = await GetClosableSessionAsync(dto.SessionId, dto.UserId, dto.IsManagerOrAdmin, cancellationToken);
        var closeHash = _idempotencyService.HashPayload(new
        {
            dto.SessionId,
            dto.UserId,
            dto.TerminalId,
            dto.TerminalCode,
            dto.CountedClosingCash,
            dto.ForceClose
        });
        var idempotency = await _idempotencyService.BeginAsync("SessionClose", dto.IdempotencyKey, closeHash, dto.UserId, session.BranchId, session.TerminalId, cancellationToken);
        if (!idempotency.IsOwner)
        {
            if (!string.IsNullOrWhiteSpace(idempotency.ErrorMessage))
            {
                throw new BusinessException(idempotency.ErrorMessage);
            }

            await transaction.CommitAsync(cancellationToken);
            return session;
        }

        await _auditLogService.LogAsync(dto.ForceClose ? "SessionForceCloseAttempted" : "SessionCloseAttempted", nameof(UserSession), session.Id.ToString(), null,
            new { dto.CountedClosingCash, dto.ForceClose, dto.IsManagerOrAdmin },
            session.BranchId, session.TerminalId, dto.UserId, cancellationToken);

        var blockers = await GetCloseBlockersAsync(session.Id, cancellationToken);
        if (blockers.Count > 0)
        {
            await _auditLogService.LogAsync("SessionCloseBlocked", nameof(UserSession), session.Id.ToString(), null,
                new { Reasons = blockers },
                session.BranchId, session.TerminalId, dto.UserId, cancellationToken);
            await _idempotencyService.FailAsync(idempotency.Record, string.Join(" ", blockers), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new BusinessException(string.Join(" ", blockers));
        }

        var expectedCash = await CalculateExpectedClosingCashAsync(session.Id, session.OpeningCashAmount, cancellationToken);
        var difference = dto.CountedClosingCash - expectedCash;
        var approvalReasons = await GetApprovalReasonsAsync(session, difference, dto, cancellationToken);

        if (approvalReasons.Count > 0 && !dto.IsManagerOrAdmin)
        {
            var oldPendingValues = new { session.Status, session.RequiresManagerApproval, session.ClosingRequestedAt };
            session.Status = SessionStatus.ClosingPending;
            session.RequiresManagerApproval = true;
            session.ClosingRequestedAt = DateTime.UtcNow;
            session.CountedClosingCash = dto.CountedClosingCash;
            session.ExpectedClosingCash = expectedCash;
            session.CashDifference = difference;
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogService.LogAsync("SessionCloseBlockedApprovalRequired", nameof(UserSession), session.Id.ToString(), oldPendingValues,
                new { session.Status, ApprovalReasons = approvalReasons, session.CountedClosingCash, session.ExpectedClosingCash, session.CashDifference },
                session.BranchId, session.TerminalId, dto.UserId, cancellationToken);
            await _idempotencyService.CompleteAsync(idempotency.Record, nameof(UserSession), session.Id, StatusCodes.Status202Accepted, session.SessionCode, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
            throw new BusinessException($"Manager/admin approval is required before closing this session: {string.Join("; ", approvalReasons)}.");
        }

        var oldValues = new { session.Status, session.EndedAt, session.CountedClosingCash, session.ExpectedClosingCash, session.CashDifference, session.RequiresManagerApproval };
        session.Status = dto.ForceClose ? SessionStatus.ForceClosed : SessionStatus.Closed;
        session.EndedAt = DateTime.UtcNow;
        session.ClosedByUserId = dto.UserId;
        session.CloseIdempotencyKey = dto.IdempotencyKey;
        session.CountedClosingCash = dto.CountedClosingCash;
        session.ExpectedClosingCash = expectedCash;
        session.CashDifference = difference;
        session.RequiresManagerApproval = false;
        await _context.SaveChangesAsync(cancellationToken);
        if (difference != 0)
        {
            await _auditLogService.LogAsync("SessionCashDifferenceDetected", nameof(UserSession), session.Id.ToString(), null,
                new { Difference = difference, Expected = expectedCash, Counted = dto.CountedClosingCash },
                session.BranchId, session.TerminalId, dto.UserId, cancellationToken);
        }
        await _auditLogService.LogAsync(dto.ForceClose ? "SessionForceClosed" : "SessionClosed", nameof(UserSession), session.Id.ToString(), oldValues,
            new { session.Status, session.EndedAt, session.CountedClosingCash, session.ExpectedClosingCash, session.CashDifference },
            session.BranchId, session.TerminalId, dto.UserId, cancellationToken);
        await _idempotencyService.CompleteAsync(idempotency.Record, nameof(UserSession), session.Id, StatusCodes.Status200OK, session.SessionCode, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
        return session;
    }

    public async Task<UserSession> ReopenSessionAsync(ReopenSessionDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new PosValidationException("Reopen reason is required.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var session = await _context.UserSessions
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == dto.SessionId, cancellationToken)
            ?? throw new PosNotFoundException("Session was not found.");

        if (session.Status is not SessionStatus.Closed and not SessionStatus.ForceClosed)
        {
            throw new BusinessException("Only a closed session can be reopened.");
        }

        if (session.TerminalId != dto.TerminalId || !string.Equals(session.TerminalCode, dto.TerminalCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Reopen must happen from the same terminal.");
        }

        if (session.StartedAt.ToLocalTime().Date != DateTime.Now.Date)
        {
            throw new BusinessException("Only same-day sessions can be reopened.");
        }

        var newerSessionExists = await _context.UserSessions.AnyAsync(x =>
            x.TerminalId == session.TerminalId &&
            x.StartedAt > session.StartedAt &&
            x.Id != session.Id, cancellationToken);
        if (newerSessionExists)
        {
            throw new BusinessException("This session cannot be reopened because a newer session has already started on this terminal.");
        }

        var blockingSession = await _context.UserSessions.AnyAsync(x =>
            x.Id != session.Id &&
            (x.UserId == session.UserId || x.TerminalId == session.TerminalId || (x.UserId == session.UserId && x.BranchId == session.BranchId)) &&
            BlockingStatuses.Contains(x.Status), cancellationToken);
        if (blockingSession)
        {
            throw new BusinessException("Close the current active session before reopening this one.");
        }

        var oldValues = new { session.Status, session.EndedAt, session.ReopenedAt, session.ReopenReason };
        session.Status = SessionStatus.Reopened;
        session.EndedAt = null;
        session.ReopenedAt = DateTime.UtcNow;
        session.ReopenedByUserId = dto.UserId;
        session.ReopenReason = dto.Reason.Trim();
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("SessionReopened", nameof(UserSession), session.Id.ToString(), oldValues,
            new { session.Status, session.ReopenedAt, session.ReopenReason },
            session.BranchId, session.TerminalId, dto.UserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
        return session;
    }

    public async Task MarkAbandonedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - (staleAfter ?? _options.SessionStaleTimeout);
        var staleSessions = await _context.UserSessions
            .Where(x => OperationalStatuses.Contains(x.Status))
            .Where(x => !_context.UserSessionHeartbeats.Any(h => h.UserSessionId == x.Id && h.LastSeenAt >= cutoff))
            .ToListAsync(cancellationToken);

        foreach (var session in staleSessions)
        {
            var oldStatus = session.Status;
            session.Status = SessionStatus.Abandoned;
            InvalidateActiveSessionCache(session.UserId, session.TerminalId, session.BranchId);
            await _auditLogService.LogAsync("SessionAbandoned", nameof(UserSession), session.Id.ToString(),
                new { Status = oldStatus },
                new { Status = SessionStatus.Abandoned },
                session.BranchId, session.TerminalId, session.UserId, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SessionSummaryViewModel> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .Include(x => x.User)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new PosNotFoundException("Session was not found. Refresh the page and try again.");

        return new SessionSummaryViewModel
        {
            Session = session,
            CompletedOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed)), cancellationToken),
            TotalSalesAmount = await _context.Orders.Where(x => x.UserSessionId == session.Id && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed))).SumAsync(x => x.TotalAmount, cancellationToken),
            CancelledOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Cancelled, cancellationToken),
            ActiveDraftOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Draft, cancellationToken),
            PurchasesCount = await _context.Purchases.CountAsync(x => x.UserSessionId == session.Id, cancellationToken),
            TotalPurchaseAmount = await _context.PurchaseItems
                .Where(x => x.Purchase!.UserSessionId == session.Id)
                .SumAsync(x => x.TotalCost, cancellationToken),
            InventoryAdjustmentsCount = await _context.InventoryMovements.CountAsync(x => x.CreatedByUserId == session.UserId && x.MovementType == InventoryMovementType.Adjustment, cancellationToken),
            LowStockWarnings = await _context.InventoryStocks.CountAsync(x => x.BranchId == session.BranchId && x.QuantityBase - x.ReservedQuantityBase <= x.InventoryItem!.ReorderLevel, cancellationToken),
            ExpectedClosingCash = session.ExpectedClosingCash ?? await CalculateExpectedClosingCashAsync(session.Id, session.OpeningCashAmount, cancellationToken),
            CountedClosingCash = session.CountedClosingCash,
            CashDifference = session.CashDifference
        };
    }

    public async Task HeartbeatAsync(int sessionId, string terminalName, CancellationToken cancellationToken = default)
    {
        var heartbeat = await _context.UserSessionHeartbeats.FirstOrDefaultAsync(x => x.UserSessionId == sessionId, cancellationToken);
        var now = DateTime.UtcNow;
        if (heartbeat is not null && heartbeat.LastSeenAt > now - _options.HeartbeatWriteInterval)
        {
            return;
        }

        if (heartbeat is null)
        {
            heartbeat = new UserSessionHeartbeat { UserSessionId = sessionId };
            _context.UserSessionHeartbeats.Add(heartbeat);
        }

        heartbeat.LastSeenAt = now;
        heartbeat.TerminalName = terminalName;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _context.ChangeTracker.Clear();
        }
    }

    private async Task<UserSession> GetClosableSessionAsync(int sessionId, string userId, bool isManagerOrAdmin, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions
            .Include(x => x.User)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == sessionId && BlockingStatuses.Contains(x.Status), cancellationToken)
            ?? throw new PosNotFoundException("Active session was not found. Start or resume a session first.");

        if (!isManagerOrAdmin && session.UserId != userId)
        {
            throw new UnauthorizedAccessException("You can only close your own session.");
        }

        return session;
    }

    private async Task<SessionCloseViewModel> BuildCloseViewModelAsync(UserSession session, CancellationToken cancellationToken)
    {
        var completed = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed)), cancellationToken);
        var drafts = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Draft, cancellationToken);
        var pending = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Pending, cancellationToken);
        var unknownFinalize = await _context.Orders.CountAsync(x =>
            x.UserSessionId == session.Id &&
            (x.OrderStatus == OrderStatus.UnknownFinalize || x.OrderStatus == OrderStatus.ReceiptFailed), cancellationToken);
        var sales = await _context.Orders
            .Where(x => x.UserSessionId == session.Id && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed)))
            .SumAsync(x => x.TotalAmount, cancellationToken);
        return new SessionCloseViewModel
        {
            Session = session,
            CompletedOrdersCount = completed,
            DraftOrdersCount = drafts,
            PendingOrdersCount = pending,
            UnknownFinalizeOrdersCount = unknownFinalize,
            TotalOrders = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id, cancellationToken),
            TotalSalesAmount = sales,
            ExpectedClosingCash = session.OpeningCashAmount + sales,
            CountedClosingCash = session.CountedClosingCash ?? 0
        };
    }

    private async Task<List<string>> GetCloseBlockersAsync(int sessionId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        var draftOrders = await _context.Orders.CountAsync(x => x.UserSessionId == sessionId && x.OrderStatus == OrderStatus.Draft, cancellationToken);
        if (draftOrders > 0)
        {
            blockers.Add($"Complete or cancel {draftOrders} held/draft order(s) before closing.");
        }

        var unknownFinalizeOrders = await _context.Orders.CountAsync(x =>
            x.UserSessionId == sessionId &&
            (x.OrderStatus == OrderStatus.UnknownFinalize || x.OrderStatus == OrderStatus.ReceiptFailed), cancellationToken);
        if (unknownFinalizeOrders > 0)
        {
            blockers.Add($"Resolve {unknownFinalizeOrders} unknown finalize/failed receipt order(s) before closing.");
        }

        return blockers;
    }

    private async Task<decimal> CalculateExpectedClosingCashAsync(int sessionId, decimal openingCash, CancellationToken cancellationToken)
    {
        var completedSales = await _context.Orders
            .Where(x => x.UserSessionId == sessionId && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed)))
            .SumAsync(x => x.TotalAmount, cancellationToken);
        return openingCash + completedSales;
    }

    private async Task<List<string>> GetApprovalReasonsAsync(UserSession session, decimal cashDifference, CloseSessionDto dto, CancellationToken cancellationToken)
    {
        var reasons = new List<string>();
        if (Math.Abs(cashDifference) > _options.SessionCashDifferenceApprovalThreshold)
        {
            reasons.Add("cash difference is above the configured threshold");
        }

        if (DateTime.UtcNow - session.StartedAt < _options.MinimumSessionDurationBeforeClose)
        {
            reasons.Add("session was started less than 5 minutes ago");
        }

        var orderCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && (x.PaymentStatus == PaymentStatus.Paid || (x.PaymentStatus == PaymentStatus.Unpaid && x.OrderStatus == OrderStatus.Completed)), cancellationToken);
        if (orderCount == 0)
        {
            reasons.Add("session has zero completed orders");
        }

        if (dto.ForceClose && session.UserId != dto.UserId)
        {
            reasons.Add("another user's session is being force-closed");
        }

        return reasons;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string GetActiveSessionCacheKey(string userId, int? terminalId, int? branchId) =>
        $"active-session:v1:user:{userId}:terminal:{terminalId?.ToString() ?? "none"}:branch:{branchId?.ToString() ?? "none"}";

    private void InvalidateActiveSessionCache(string userId, int terminalId, int branchId)
    {
        _cache.Remove(GetActiveSessionCacheKey(userId, terminalId, branchId));
        _cache.Remove(GetActiveSessionCacheKey(userId, null, null));
    }

    private static UserSession CloneSession(UserSession session) =>
        new()
        {
            Id = session.Id,
            PublicId = session.PublicId,
            IdempotencyKey = session.IdempotencyKey,
            CloseIdempotencyKey = session.CloseIdempotencyKey,
            SessionCode = session.SessionCode,
            UserId = session.UserId,
            BranchId = session.BranchId,
            Branch = session.Branch is null
                ? null
                : new Branch
                {
                    Id = session.Branch.Id,
                    Name = session.Branch.Name,
                    BranchCode = session.Branch.BranchCode,
                    IsActive = session.Branch.IsActive
                },
            RoleName = session.RoleName,
            TerminalName = session.TerminalName,
            TerminalId = session.TerminalId,
            TerminalCode = session.TerminalCode,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Status = session.Status,
            OpeningCashAmount = session.OpeningCashAmount,
            CountedClosingCash = session.CountedClosingCash,
            ExpectedClosingCash = session.ExpectedClosingCash,
            CashDifference = session.CashDifference,
            RequiresManagerApproval = session.RequiresManagerApproval,
            ClosingRequestedAt = session.ClosingRequestedAt,
            ClosedByUserId = session.ClosedByUserId,
            ReopenedByUserId = session.ReopenedByUserId,
            ReopenedAt = session.ReopenedAt,
            ReopenReason = session.ReopenReason,
            Notes = session.Notes,
            CreatedAt = session.CreatedAt,
            IsSynced = session.IsSynced,
            SyncedAt = session.SyncedAt
        };
}
