namespace BranchPOS.Services;

public interface ISessionCodeGeneratorService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
