using BranchPOS.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BranchPOS.Services;

public static class DatabaseErrorTranslator
{
    public static bool IsConcurrencyFailure(Exception exception) =>
        FindPostgresException(exception) is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };

    public static bool IsUniqueViolation(Exception exception) =>
        FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation };

    public static BranchPosException ToUserException(Exception exception, string fallbackMessage)
    {
        var postgresException = FindPostgresException(exception);
        if (postgresException is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
        {
            return new PosConcurrencyException(
                "Another terminal completed a stock change first. Please retry.",
                "PostgreSQL concurrency conflict during POS transaction.",
                exception);
        }

        if (postgresException is { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            var userMessage = postgresException.ConstraintName switch
            {
                "UX_Branches_BranchCode" => "Branch code already exists.",
                "UX_Terminals_TerminalCode" => "Terminal code already exists.",
                "UX_Customers_BranchId_PhoneNumber" => "Customer already exists for this branch.",
                "UX_UserSessions_UserId_Active" => "You already have an active session.",
                "UX_UserSessions_SessionCode" => "Session code already exists. Please retry.",
                "UX_Orders_BranchId_OrderNumber" => "Order number already exists. Please retry.",
                "UX_TerminalHeartbeats_TerminalId" => "Terminal heartbeat already exists. Please retry.",
                "UX_UserSessionHeartbeats_UserSessionId" => "Session heartbeat already exists. Please retry.",
                _ => fallbackMessage
            };

            return new BusinessException(userMessage, $"PostgreSQL unique constraint violation: {postgresException.ConstraintName}.", exception);
        }

        return new BusinessException(fallbackMessage, "Database operation failed.", exception);
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            return postgresException;
        }

        if (exception is DbUpdateException { InnerException: PostgresException updatePostgresException })
        {
            return updatePostgresException;
        }

        return exception.InnerException is null ? null : FindPostgresException(exception.InnerException);
    }
}
