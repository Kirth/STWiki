using STWiki.Models.Collaboration.Operations;

namespace STWiki.Models.Collaboration;

/// <summary>
/// Result of processing a text operation
/// </summary>
public sealed record OperationResult(
    bool Success,
    ITextOperation? ProcessedOperation,
    string? ErrorMessage = null,
    OperationErrorType ErrorType = OperationErrorType.None,
    ITextOperation? OriginalOperation = null
)
{
    /// <summary>
    /// Create a successful result
    /// </summary>
    public static OperationResult CreateSuccess(ITextOperation processedOperation, ITextOperation? originalOperation = null)
    {
        return new OperationResult(true, processedOperation, OriginalOperation: originalOperation);
    }
    
    /// <summary>
    /// Create a failed result
    /// </summary>
    public static OperationResult Failure(string errorMessage, OperationErrorType errorType = OperationErrorType.ValidationError, ITextOperation? originalOperation = null)
    {
        return new OperationResult(false, null, errorMessage, errorType, originalOperation);
    }
    
    /// <summary>
    /// Create a conflict result (operation needs to be retried with transformation)
    /// </summary>
    public static OperationResult Conflict(string errorMessage, ITextOperation originalOperation)
    {
        return new OperationResult(false, null, errorMessage, OperationErrorType.Conflict, originalOperation);
    }
}

/// <summary>
/// Types of operation errors
/// </summary>
public enum OperationErrorType
{
    None,
    ValidationError,
    Conflict,
    ServerError,
    NetworkError,
    Timeout
}

/// <summary>
/// Result of validating a text operation
/// </summary>
public sealed record OperationValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    OperationValidationError ErrorType = OperationValidationError.None
)
{
    /// <summary>
    /// Create a valid result
    /// </summary>
    public static OperationValidationResult Valid()
    {
        return new OperationValidationResult(true);
    }
    
    /// <summary>
    /// Create an invalid result
    /// </summary>
    public static OperationValidationResult Invalid(string errorMessage, OperationValidationError errorType)
    {
        return new OperationValidationResult(false, errorMessage, errorType);
    }
}

/// <summary>
/// Types of operation validation errors
/// </summary>
public enum OperationValidationError
{
    None,
    InvalidPosition,
    InvalidLength,
    InvalidContent,
    OutOfBounds,
    MissingContent,
    InvalidSelection
}