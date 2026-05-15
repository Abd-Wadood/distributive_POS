using BranchPOS.Models;
using BranchPOS.ViewModels;

namespace BranchPOS.Services;

public interface ITerminalService
{
    Task<TerminalAdminViewModel> BuildAdminModelAsync(TerminalCreateViewModel createModel, string userId, CancellationToken cancellationToken = default);

    Task<TerminalEditViewModel> BuildEditModelAsync(int id, string userId, CancellationToken cancellationToken = default);

    Task CreateAsync(TerminalCreateViewModel model, string userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, TerminalEditViewModel model, string userId, CancellationToken cancellationToken = default);

    Task ToggleAsync(int id, string userId, CancellationToken cancellationToken = default);
}
