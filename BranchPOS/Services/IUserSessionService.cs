using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface IUserSessionService
{
    Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default);

    Task<UserSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSession?> GetAbandonedSessionAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSession?> GetActiveSessionForTerminalAsync(int terminalId, CancellationToken cancellationToken = default);

    Task<UserSession> ContinueSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);

    Task<SessionCloseViewModel> GetCloseSessionAsync(int sessionId, string userId, bool isManagerOrAdmin, CancellationToken cancellationToken = default);

    Task<UserSession> CloseSessionAsync(CloseSessionDto dto, CancellationToken cancellationToken = default);

    Task<UserSession> ReopenSessionAsync(ReopenSessionDto dto, CancellationToken cancellationToken = default);

    Task MarkAbandonedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default);

    Task<SessionSummaryViewModel> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default);

    Task HeartbeatAsync(int sessionId, string terminalName, CancellationToken cancellationToken = default);
}
