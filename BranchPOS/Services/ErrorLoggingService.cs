using System.Security.Claims;

namespace BranchPOS.Services;

public class ErrorLoggingService : IErrorLoggingService
{
    private readonly ILogger<ErrorLoggingService> _logger;

    public ErrorLoggingService(ILogger<ErrorLoggingService> logger)
    {
        _logger = logger;
    }

    public void LogException(HttpContext httpContext, Exception exception, string? userMessage = null)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var terminalCode = httpContext.Request.Cookies[TerminalContextService.TerminalCodeCookieName];

        _logger.LogError(exception,
            "BranchPOS error at {TimestampUtc}. RequestId={RequestId} TraceId={TraceId} UserId={UserId} TerminalCode={TerminalCode} Path={Path} Method={Method} SafeMessage={SafeMessage}",
            DateTime.UtcNow,
            httpContext.TraceIdentifier,
            httpContext.TraceIdentifier,
            userId,
            terminalCode,
            httpContext.Request.Path.Value,
            httpContext.Request.Method,
            userMessage);
    }
}
