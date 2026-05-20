namespace BranchPOS.Services;

public sealed class PosMenuCacheInvalidator : IPosMenuCacheInvalidator, IDisposable
{
    private CancellationTokenSource _current = new();

    public CancellationToken CurrentToken => _current.Token;

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _current, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    public void Dispose()
    {
        _current.Cancel();
        _current.Dispose();
    }
}
