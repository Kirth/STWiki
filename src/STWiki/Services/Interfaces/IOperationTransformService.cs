using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Service for performing operational transformation on text operations
/// </summary>
public interface IOperationTransformService
{
    /// <summary>
    /// Transform an operation against another operation
    /// </summary>
    T Transform<T>(T operation, ITextOperation other) where T : ITextOperation;
    
    /// <summary>
    /// Transform an operation against a list of operations (operation history)
    /// </summary>
    ITextOperation TransformAgainstHistory(ITextOperation operation, IEnumerable<ITextOperation> history);
    
    /// <summary>
    /// Validate that an operation is valid and can be applied
    /// </summary>
    OperationValidationResult ValidateOperation(ITextOperation operation, string currentContent);
    
    /// <summary>
    /// Check if two operations conflict with each other
    /// </summary>
    bool DoOperationsConflict(ITextOperation op1, ITextOperation op2);
}