namespace BranchPOS.Services;

public interface IErrorLoggingService
{
    void LogException(HttpContext httpContext, Exception exception, string? userMessage = null);
}
