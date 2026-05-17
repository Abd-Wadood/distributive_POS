using BranchPOS.Exceptions;
using BranchPOS.Services;

namespace BranchPOS.Middleware;

public class FriendlyExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public FriendlyExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IErrorLoggingService errorLoggingService)
    {
        try
        {
            await _next(context);
        }
        catch (BranchPosException ex)
        {
            errorLoggingService.LogException(context, ex, ex.UserMessage);
            await WriteFriendlyResponseAsync(context, StatusCodes.Status400BadRequest, ex.UserMessage);
        }
        catch (UnauthorizedAccessException ex)
        {
            const string message = "You do not have permission to access this section.";
            errorLoggingService.LogException(context, ex, message);
            await WriteFriendlyResponseAsync(context, StatusCodes.Status403Forbidden, message);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            const string message = "Request body is too large. Please reduce the submitted data and try again.";
            errorLoggingService.LogException(context, ex, message);
            await WriteFriendlyResponseAsync(context, StatusCodes.Status413PayloadTooLarge, message);
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            const string message = "Request timed out. Please try again.";
            errorLoggingService.LogException(context, ex, message);
            await WriteFriendlyResponseAsync(context, StatusCodes.Status503ServiceUnavailable, message);
        }
        catch (Exception ex)
        {
            const string message = "Something went wrong. Please try again. Contact administrator if the issue continues.";
            errorLoggingService.LogException(context, ex, message);
            await WriteFriendlyResponseAsync(context, StatusCodes.Status500InternalServerError, message);
        }
    }

    private static async Task WriteFriendlyResponseAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        if (IsJsonRequest(context))
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message,
                requestId = context.TraceIdentifier
            });
            return;
        }

        context.Response.Redirect($"/Home/Error?requestId={Uri.EscapeDataString(context.TraceIdentifier)}&message={Uri.EscapeDataString(message)}");
    }

    private static bool IsJsonRequest(HttpContext context) =>
        context.Request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true) ||
        context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true ||
        context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
