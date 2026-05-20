namespace BranchPOS.Services;

public interface IPosMenuCacheInvalidator
{
    CancellationToken CurrentToken { get; }

    void Invalidate();
}
