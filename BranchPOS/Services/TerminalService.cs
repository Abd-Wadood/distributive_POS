using BranchPOS.Data;
using BranchPOS.Exceptions;
using BranchPOS.Models;
using BranchPOS.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class TerminalService : ITerminalService
{
    private const int HeartbeatDisplayLimit = 100;

    private readonly AppDbContext _context;
    private readonly IBranchService _branchService;
    private readonly IAuditLogService _auditLogService;

    public TerminalService(AppDbContext context, IBranchService branchService, IAuditLogService auditLogService)
    {
        _context = context;
        _branchService = branchService;
        _auditLogService = auditLogService;
    }

    public async Task<TerminalAdminViewModel> BuildAdminModelAsync(TerminalCreateViewModel createModel, string userId, CancellationToken cancellationToken = default)
    {
        var branches = await _branchService.GetBranchesForUserAsync(userId, cancellationToken);
        if (createModel.BranchId <= 0)
        {
            createModel.BranchId = branches.FirstOrDefault()?.Id ?? 1;
        }

        createModel.Branches = branches
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == createModel.BranchId))
            .ToList();

        return new TerminalAdminViewModel
        {
            NewTerminal = createModel,
            Terminals = await _context.Terminals
                .Include(x => x.Branch)
                .OrderBy(x => x.Branch!.Name)
                .ThenBy(x => x.TerminalCode)
                .ToListAsync(cancellationToken),
            Heartbeats = await _context.TerminalHeartbeats
                .Include(x => x.Terminal)
                .Include(x => x.Branch)
                .Include(x => x.CurrentUser)
                .Include(x => x.CurrentSession)
                .OrderByDescending(x => x.LastSeenAt)
                .Take(HeartbeatDisplayLimit)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<TerminalEditViewModel> BuildEditModelAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new PosNotFoundException("Terminal was not found. Refresh the page and try again.");

        await _branchService.EnsureBranchAccessAsync(userId, terminal.BranchId, cancellationToken);
        var model = new TerminalEditViewModel
        {
            Id = terminal.Id,
            TerminalCode = terminal.TerminalCode,
            BranchId = terminal.BranchId,
            Name = terminal.Name,
            IpAddress = terminal.IpAddress,
            IsActive = terminal.IsActive
        };

        var branches = await _branchService.GetBranchesForUserAsync(userId, cancellationToken);
        model.Branches = branches.Select(x => new SelectListItem(x.Name, x.Id.ToString(), x.Id == model.BranchId)).ToList();
        return model;
    }

    public async Task CreateAsync(TerminalCreateViewModel model, string userId, CancellationToken cancellationToken = default)
    {
        model.TerminalCode = TerminalContextService.NormalizeCode(model.TerminalCode);
        ValidateModel(model);
        await _branchService.EnsureBranchAccessAsync(userId, model.BranchId, cancellationToken);

        var token = TerminalContextService.GenerateTerminalToken();
        var terminal = new Terminal
        {
            TerminalCode = model.TerminalCode,
            BranchId = model.BranchId,
            Name = model.Name.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(model.IpAddress) ? null : model.IpAddress.Trim(),
            TerminalTokenHash = TerminalContextService.HashTerminalToken(token)
        };

        _context.Terminals.Add(terminal);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            throw DatabaseErrorTranslator.ToUserException(ex, "Terminal code already exists.");
        }

        await _auditLogService.LogAsync("TerminalCreated", nameof(Terminal), terminal.Id.ToString(), null,
            new { terminal.TerminalCode, terminal.BranchId, terminal.Name, terminal.IpAddress, terminal.IsActive },
            terminal.BranchId, terminal.Id, userId, cancellationToken);
    }

    public async Task UpdateAsync(int id, TerminalEditViewModel model, string userId, CancellationToken cancellationToken = default)
    {
        if (id != model.Id)
        {
            throw new PosValidationException("Terminal request is invalid. Refresh and try again.");
        }

        model.TerminalCode = TerminalContextService.NormalizeCode(model.TerminalCode);
        ValidateModel(model);
        await _branchService.EnsureBranchAccessAsync(userId, model.BranchId, cancellationToken);

        var terminal = await _context.Terminals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new PosNotFoundException("Terminal was not found. Refresh the page and try again.");

        var oldValues = new { terminal.TerminalCode, terminal.BranchId, terminal.Name, terminal.IpAddress, terminal.IsActive };
        terminal.TerminalCode = model.TerminalCode;
        terminal.BranchId = model.BranchId;
        terminal.Name = model.Name.Trim();
        terminal.IpAddress = string.IsNullOrWhiteSpace(model.IpAddress) ? null : model.IpAddress.Trim();
        terminal.IsActive = model.IsActive;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorTranslator.IsUniqueViolation(ex))
        {
            throw DatabaseErrorTranslator.ToUserException(ex, "Terminal code already exists.");
        }

        await _auditLogService.LogAsync("TerminalUpdated", nameof(Terminal), terminal.Id.ToString(), oldValues,
            new { terminal.TerminalCode, terminal.BranchId, terminal.Name, terminal.IpAddress, terminal.IsActive },
            terminal.BranchId, terminal.Id, userId, cancellationToken);
    }

    public async Task ToggleAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new PosNotFoundException("Terminal was not found. Refresh the page and try again.");

        await _branchService.EnsureBranchAccessAsync(userId, terminal.BranchId, cancellationToken);
        var oldValues = new { terminal.IsActive };
        terminal.IsActive = !terminal.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("TerminalToggled", nameof(Terminal), terminal.Id.ToString(), oldValues,
            new { terminal.IsActive }, terminal.BranchId, terminal.Id, userId, cancellationToken);
    }

    private static void ValidateModel(TerminalCreateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TerminalCode))
        {
            throw new PosValidationException("Terminal code is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new PosValidationException("Terminal name is required.");
        }
    }
}
