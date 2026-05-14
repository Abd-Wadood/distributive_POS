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
        if (IsConcurrencyFailure(exception))
        {
            return new PosConcurrencyException(
                "Another terminal completed a stock change first. Please retry.",
                "PostgreSQL concurrency conflict during POS transaction.",
                exception);
        }

        if (IsUniqueViolation(exception))
        {
            return new BusinessException(fallbackMessage, "PostgreSQL unique constraint violation.", exception);
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
