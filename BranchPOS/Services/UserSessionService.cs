using BranchPOS.Data;
using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BranchPOS.Services;

public class UserSessionService : IUserSessionService
{
    private readonly AppDbContext _context;
    private readonly IBranchService _branchService;

    public UserSessionService(AppDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
    }

    public async Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.TerminalId <= 0 || string.IsNullOrWhiteSpace(dto.TerminalCode))
        {
            throw new InvalidOperationException("Terminal is not registered.");
        }

        await _branchService.EnsureBranchAccessAsync(dto.UserId, dto.BranchId, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0}))", [dto.UserId], cancellationToken);

        var active = await GetActiveSessionAsync(dto.UserId, cancellationToken);
        if (active is not null)
        {
            throw new InvalidOperationException($"You already have an active session ({active.SessionCode}). Continue or end it before starting a new session.");
        }

        var session = new UserSession
        {
            SessionCode = await GenerateSessionCodeAsync(cancellationToken),
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
            throw new InvalidOperationException("You already have an active session. Continue or end it before starting a new session.", ex);
        }

        await HeartbeatAsync(session.Id, session.TerminalName, cancellationToken);
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
            throw new InvalidOperationException("Session was not found.");
        }

        var active = await GetActiveSessionAsync(userId, cancellationToken);
        if (active is not null && active.Id != session.Id)
        {
            throw new InvalidOperationException("End the current active session before resuming another session.");
        }

        session.Status = SessionStatus.Active;
        session.EndedAt = null;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException("End the current active session before resuming another session.", ex);
        }

        await HeartbeatAsync(session.Id, session.TerminalName, cancellationToken);
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
            throw new InvalidOperationException("Active session was not found.");
        }

        var activeDrafts = await _context.Orders.CountAsync(x =>
            x.UserSessionId == session.Id &&
            x.OrderStatus == OrderStatus.Draft, cancellationToken);

        if (activeDrafts > 0)
        {
            throw new InvalidOperationException("Complete or cancel active draft orders before ending the session.");
        }

        session.Status = SessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkInterruptedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - (staleAfter ?? TimeSpan.FromMinutes(2));
        var staleSessions = await _context.UserSessions
            .Where(x => x.Status == SessionStatus.Active)
            .Where(x => !_context.UserSessionHeartbeats.Any(h => h.UserSessionId == x.Id && h.LastSeenAt >= cutoff))
            .ToListAsync(cancellationToken);

        foreach (var session in staleSessions)
        {
            session.Status = SessionStatus.Interrupted;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SessionSummaryViewModel> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .Include(x => x.User)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session was not found.");

        var purchases = await _context.Purchases
            .Include(x => x.Items)
            .Where(x => x.UserSessionId == session.Id)
            .ToListAsync(cancellationToken);

        return new SessionSummaryViewModel
        {
            Session = session,
            CompletedOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Completed, cancellationToken),
            TotalSalesAmount = await _context.Orders.Where(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Completed).SumAsync(x => x.TotalAmount, cancellationToken),
            CancelledOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Cancelled, cancellationToken),
            ActiveDraftOrdersCount = await _context.Orders.CountAsync(x => x.UserSessionId == session.Id && x.OrderStatus == OrderStatus.Draft, cancellationToken),
            PurchasesCount = purchases.Count,
            TotalPurchaseAmount = purchases.Sum(x => x.Items.Sum(i => i.Quantity * i.UnitCost)),
            InventoryAdjustmentsCount = await _context.InventoryTransactions.CountAsync(x => x.UserSessionId == session.Id && x.TransactionType == InventoryTransactionType.Adjustment, cancellationToken),
            LowStockWarnings = await _context.Inventories.CountAsync(x => x.BranchId == session.BranchId && x.CurrentQuantity <= x.Ingredient!.MinimumStockLevel, cancellationToken)
        };
    }

    public async Task HeartbeatAsync(int sessionId, string terminalName, CancellationToken cancellationToken = default)
    {
        var heartbeat = await _context.UserSessionHeartbeats.FirstOrDefaultAsync(x => x.UserSessionId == sessionId, cancellationToken);
        if (heartbeat is null)
        {
            heartbeat = new UserSessionHeartbeat { UserSessionId = sessionId };
            _context.UserSessionHeartbeats.Add(heartbeat);
        }

        heartbeat.LastSeenAt = DateTime.UtcNow;
        heartbeat.TerminalName = terminalName;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateSessionCodeAsync(CancellationToken cancellationToken)
    {
        var prefix = $"SES-{DateTime.UtcNow:yyyyMMdd}";
        var count = await _context.UserSessions.CountAsync(x => x.SessionCode.StartsWith(prefix), cancellationToken) + 1;
        return $"{prefix}-{count:0000}";
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
