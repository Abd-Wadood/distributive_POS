namespace BranchPOS.Exceptions;

public abstract class BranchPosException : InvalidOperationException
{
    protected BranchPosException(string userMessage, string? logMessage = null, Exception? innerException = null)
        : base(logMessage ?? userMessage, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}

public class BusinessException : BranchPosException
{
    public BusinessException(string userMessage, string? logMessage = null, Exception? innerException = null)
        : base(userMessage, logMessage, innerException)
    {
    }
}

public class PosValidationException : BranchPosException
{
    public PosValidationException(string userMessage, string? logMessage = null, Exception? innerException = null)
        : base(userMessage, logMessage, innerException)
    {
    }
}

public class PosNotFoundException : BranchPosException
{
    public PosNotFoundException(string userMessage, string? logMessage = null, Exception? innerException = null)
        : base(userMessage, logMessage, innerException)
    {
    }
}

public class PosConcurrencyException : BranchPosException
{
    public PosConcurrencyException(string userMessage, string? logMessage = null, Exception? innerException = null)
        : base(userMessage, logMessage, innerException)
    {
    }
}
