using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
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
    private readonly PosOperationalOptions _options;

    public UserSessionService(
        AppDbContext context,
        IBranchService branchService,
        ITerminalContextService terminalContextService,
        ISessionCodeGeneratorService sessionCodeGenerator,
        IAuditLogService auditLogService,
        IOptions<PosOperationalOptions> options)
    {
        _context = context;
        _branchService = branchService;
        _terminalContextService = terminalContextService;
        _sessionCodeGenerator = sessionCodeGenerator;
        _auditLogService = auditLogService;
        _options = options.Value;
    }

    public async Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new BusinessException("Terminal is not registered. Register this terminal before starting a session.");
        }

        await _branchService.EnsureBranchAccessAsync(dto.UserId, dto.BranchId, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [dto.UserId], cancellationToken);

        var active = await GetActiveSessionAsync(dto.UserId, cancellationToken);
        if (active is not null)
        {
            throw new BusinessException($"You already have an active session ({active.SessionCode}). Continue or end it before starting a new session.");
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
            Notes = dto.Notes
        };

        _context.UserSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw DatabaseErrorTranslator.ToUserException(ex, "You already have an active session. Continue or end it before starting a new session.");
        }

        await HeartbeatAsync(session.Id, session.TerminalName, cancellationToken);
        await _auditLogService.LogAsync("SessionStarted", nameof(UserSession), session.Id.ToString(), null,
            new { session.SessionCode, session.BranchId, session.TerminalId, session.TerminalCode, session.RoleName },
            session.BranchId, session.TerminalId, session.UserId, cancellationToken);
        return session;
    }

    public Task<UserSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Include(x => x.Branch)
            .Where(x => x.UserId == userId && x.Status == SessionStatus.Active)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserSession?> GetInterruptedSessionAsync(string userId, CancellationToken cancellationToken = default) =>
        _context.UserSessions
            .Include(x => x.Branch)
            .Where(x => x.UserId == userId && x.Status == SessionStatus.Interrupted)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSession> ContinueSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == sessionId &&
            x.UserId == userId &&
            (x.Status == SessionStatus.Active || x.Status == SessionStatus.Interrupted), cancellationToken);

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

        session.Status = SessionStatus.Active;
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
        return session;
    }

    public async Task EndSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x =>
            x.Id == sessionId &&
            x.UserId == userId &&
            x.Status == SessionStatus.Active, cancellationToken);

        if (session is null)
        {
            throw new PosNotFoundException("Active session was not found. Start or resume a session first.");
        }

        var activeDrafts = await _context.Orders.CountAsync(x =>
            x.UserSessionId == session.Id &&
            x.OrderStatus == OrderStatus.Draft, cancellationToken);

        if (activeDrafts > 0)
        {
            throw new BusinessException("Complete or cancel active draft orders before ending the session.");
        }

        var oldValues = new { session.Status, session.EndedAt };
        session.Status = SessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("SessionEnded", nameof(UserSession), session.Id.ToString(), oldValues,
            new { session.Status, session.EndedAt },
            session.BranchId, session.TerminalId, session.UserId, cancellationToken);
    }

    public async Task MarkInterruptedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - (staleAfter ?? _options.SessionStaleTimeout);
        var staleSessions = await _context.UserSessions
            .Where(x => x.Status == SessionStatus.Active)
            .Where(x => !_context.UserSessionHeartbeats.Any(h => h.UserSessionId == x.Id && h.LastSeenAt >= cutoff))
            .ToListAsync(cancellationToken);

        foreach (var session in staleSessions)
        {
            session.Status = SessionStatus.Interrupted;
            await _auditLogService.LogAsync("SessionInterrupted", nameof(UserSession), session.Id.ToString(),
                new { Status = SessionStatus.Active },
                new { Status = SessionStatus.Interrupted },
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
            CompletedOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Completed, cancellationToken),
            TotalSalesAmount = await _context.Orders.Where(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Completed).SumAsync(x => x.TotalAmount, cancellationToken),
            CancelledOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Cancelled, cancellationToken),
            ActiveDraftOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Draft, cancellationToken),
            PurchasesCount = await _context.Purchases.CountAsync(x => x.UserSessionId == session.Id, cancellationToken),
            TotalPurchaseAmount = await _context.PurchaseItems
                .Where(x => x.Purchase!.UserSessionId == session.Id)
                .SumAsync(x => x.Quantity * x.UnitCost, cancellationToken),
            InventoryAdjustmentsCount = await _context.InventoryTransactions.CountAsync(x => x.UserSessionId == session.Id && x.TransactionType == InventoryTransactionType.Adjustment, cancellationToken),
            LowStockWarnings = await _context.Inventories.CountAsync(x => x.BranchId == session.BranchId && x.CurrentQuantity <= x.Ingredient!.MinimumStockLevel, cancellationToken)
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
