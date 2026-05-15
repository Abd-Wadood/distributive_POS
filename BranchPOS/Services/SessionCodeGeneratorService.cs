using BranchPOS.Data;
using Microsoft.EntityFrameworkCore;

namespace BranchPOS.Services;

public class SessionCodeGeneratorService : ISessionCodeGeneratorService
{
    private readonly AppDbContext _context;

    public SessionCodeGeneratorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var next = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"SessionCodeSequence\"') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return $"SES-{DateTime.UtcNow:yyyyMMdd}-{next:000000}";
    }
}
