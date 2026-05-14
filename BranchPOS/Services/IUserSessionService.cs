using BranchPOS.DTOs;
using BranchPOS.Models;
using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface IUserSessionService
{
    Task<UserSession> StartSessionAsync(StartSessionDto dto, CancellationToken cancellationToken = default);

    Task<UserSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSession?> GetInterruptedSessionAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserSession> ContinueSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);

    Task EndSessionAsync(int sessionId, string userId, CancellationToken cancellationToken = default);

    Task MarkInterruptedSessionsAsync(TimeSpan? staleAfter = null, CancellationToken cancellationToken = default);

    Task<SessionSummaryViewModel> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default);

    Task HeartbeatAsync(int sessionId, string terminalName, CancellationToken cancellationToken = default);
}
