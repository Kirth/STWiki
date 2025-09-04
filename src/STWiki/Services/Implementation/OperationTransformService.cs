using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of operational transform service
/// </summary>
public class OperationTransformService : IOperationTransformService
{
    private readonly ILogger<OperationTransformService> _logger;
    
    public OperationTransformService(ILogger<OperationTransformService> logger)
    {
        _logger = logger;
    }
    
    public OperationResult TransformOperation(ITextOperation operation, ITextOperation againstOperation)
    {
        try
        {
            _logger.LogDebug("Transforming operation {Op1} against {Op2}", 
                operation.GetType().Name, againstOperation.GetType().Name);
            
            var transformed = (operation, againstOperation) switch
            {
                // Insert vs Insert
                (InsertOperation op1, InsertOperation op2) => TransformInsertVsInsert(op1, op2),
                
                // Insert vs Delete
                (InsertOperation op1, DeleteOperation op2) => TransformInsertVsDelete(op1, op2),
                (DeleteOperation op1, InsertOperation op2) => TransformDeleteVsInsert(op1, op2),
                
                // Insert vs Replace
                (InsertOperation op1, ReplaceOperation op2) => TransformInsertVsReplace(op1, op2),
                (ReplaceOperation op1, InsertOperation op2) => TransformReplaceVsInsert(op1, op2),
                
                // Delete vs Delete
                (DeleteOperation op1, DeleteOperation op2) => TransformDeleteVsDelete(op1, op2),
                
                // Delete vs Replace
                (DeleteOperation op1, ReplaceOperation op2) => TransformDeleteVsReplace(op1, op2),
                (ReplaceOperation op1, DeleteOperation op2) => TransformReplaceVsDelete(op1, op2),
                
                // Replace vs Replace
                (ReplaceOperation op1, ReplaceOperation op2) => TransformReplaceVsReplace(op1, op2),
                
                _ => throw new NotSupportedException($"Transform not supported for {operation.GetType().Name} vs {againstOperation.GetType().Name}")
            };
            
            return OperationResult.CreateSuccess(transformed, operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming operations");
            return OperationResult.Failure($"Transform failed: {ex.Message}", OperationErrorType.ServerError, operation);
        }
    }
    
    public OperationValidationResult ValidateOperation(ITextOperation operation, string currentContent)
    {
        try
        {
            return operation switch
            {
                InsertOperation insertOp => ValidateInsertOperation(insertOp, currentContent),
                DeleteOperation deleteOp => ValidateDeleteOperation(deleteOp, currentContent),
                ReplaceOperation replaceOp => ValidateReplaceOperation(replaceOp, currentContent),
                _ => OperationValidationResult.Invalid("Unknown operation type", OperationValidationError.InvalidContent)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating operation {OperationId}", operation.OperationId);
            return OperationValidationResult.Invalid($"Validation error: {ex.Message}", OperationValidationError.InvalidContent);
        }
    }
    
    public T Transform<T>(T operation, ITextOperation other) where T : ITextOperation
    {
        var result = TransformOperation(operation, other);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Transform failed: {result.ErrorMessage}");
        }
        return (T)result.ProcessedOperation!;
    }
    
    public ITextOperation TransformAgainstHistory(ITextOperation operation, IEnumerable<ITextOperation> history)
    {
        var transformedOperation = operation;
        foreach (var historicalOperation in history)
        {
            var result = TransformOperation(transformedOperation, historicalOperation);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Transform against history failed: {result.ErrorMessage}");
            }
            transformedOperation = result.ProcessedOperation!;
        }
        return transformedOperation;
    }
    
    public bool DoOperationsConflict(ITextOperation op1, ITextOperation op2)
    {
        return (op1, op2) switch
        {
            // Two operations conflict if they overlap in any way
            (InsertOperation i1, InsertOperation i2) => i1.Position == i2.Position,
            (DeleteOperation d1, DeleteOperation d2) => DoRangesOverlap(d1.Position, d1.Position + d1.Length, d2.Position, d2.Position + d2.Length),
            (ReplaceOperation r1, ReplaceOperation r2) => DoRangesOverlap(r1.SelectionStart, r1.SelectionEnd, r2.SelectionStart, r2.SelectionEnd),
            (InsertOperation i, DeleteOperation d) => i.Position >= d.Position && i.Position <= d.Position + d.Length,
            (DeleteOperation d, InsertOperation i) => i.Position >= d.Position && i.Position <= d.Position + d.Length,
            (InsertOperation i, ReplaceOperation r) => i.Position >= r.SelectionStart && i.Position <= r.SelectionEnd,
            (ReplaceOperation r, InsertOperation i) => i.Position >= r.SelectionStart && i.Position <= r.SelectionEnd,
            (DeleteOperation d, ReplaceOperation r) => DoRangesOverlap(d.Position, d.Position + d.Length, r.SelectionStart, r.SelectionEnd),
            (ReplaceOperation r, DeleteOperation d) => DoRangesOverlap(d.Position, d.Position + d.Length, r.SelectionStart, r.SelectionEnd),
            _ => false
        };
    }
    
    private static bool DoRangesOverlap(int start1, int end1, int start2, int end2)
    {
        return start1 < end2 && start2 < end1;
    }
    
    // Transform Insert vs Insert
    private ITextOperation TransformInsertVsInsert(InsertOperation op1, InsertOperation op2)
    {
        if (op1.Position <= op2.Position)
        {
            // op1 is before or at the same position as op2, no change needed
            return op1;
        }
        else
        {
            // op1 is after op2, adjust position
            return op1 with { Position = op1.Position + op2.Content.Length };
        }
    }
    
    // Transform Insert vs Delete
    private ITextOperation TransformInsertVsDelete(InsertOperation op1, DeleteOperation op2)
    {
        if (op1.Position <= op2.Position)
        {
            // Insert is before delete position, no change needed
            return op1;
        }
        else if (op1.Position >= op2.Position + op2.Length)
        {
            // Insert is after deleted range, adjust position
            return op1 with { Position = op1.Position - op2.Length };
        }
        else
        {
            // Insert is within deleted range, move to delete position
            return op1 with { Position = op2.Position };
        }
    }
    
    // Transform Delete vs Insert
    private ITextOperation TransformDeleteVsInsert(DeleteOperation op1, InsertOperation op2)
    {
        if (op2.Position <= op1.Position)
        {
            // Insert is before delete, adjust delete position
            return op1 with { Position = op1.Position + op2.Content.Length };
        }
        else if (op2.Position >= op1.Position + op1.Length)
        {
            // Insert is after delete, no change needed
            return op1;
        }
        else
        {
            // Insert is within delete range, no change needed (delete will remove the inserted content)
            return op1;
        }
    }
    
    // Transform Insert vs Replace
    private ITextOperation TransformInsertVsReplace(InsertOperation op1, ReplaceOperation op2)
    {
        if (op1.Position <= op2.SelectionStart)
        {
            // Insert is before replace selection
            return op1;
        }
        else if (op1.Position >= op2.SelectionEnd)
        {
            // Insert is after replace selection, adjust for length difference
            var lengthDiff = op2.NewContent.Length - op2.SelectionLength;
            return op1 with { Position = op1.Position + lengthDiff };
        }
        else
        {
            // Insert is within replace selection, move to start of replace
            return op1 with { Position = op2.SelectionStart };
        }
    }
    
    // Transform Replace vs Insert
    private ITextOperation TransformReplaceVsInsert(ReplaceOperation op1, InsertOperation op2)
    {
        if (op2.Position <= op1.SelectionStart)
        {
            // Insert is before replace selection, adjust selection bounds
            var adjustment = op2.Content.Length;
            return op1 with 
            { 
                SelectionStart = op1.SelectionStart + adjustment,
                SelectionEnd = op1.SelectionEnd + adjustment
            };
        }
        else if (op2.Position >= op1.SelectionEnd)
        {
            // Insert is after replace selection, no change needed
            return op1;
        }
        else
        {
            // Insert is within replace selection, expand selection end
            return op1 with { SelectionEnd = op1.SelectionEnd + op2.Content.Length };
        }
    }
    
    // Transform Delete vs Delete
    private ITextOperation TransformDeleteVsDelete(DeleteOperation op1, DeleteOperation op2)
    {
        if (op1.Position >= op2.Position + op2.Length)
        {
            // op1 is after op2, adjust position
            return op1 with { Position = op1.Position - op2.Length };
        }
        else if (op1.Position + op1.Length <= op2.Position)
        {
            // op1 is before op2, no change needed
            return op1;
        }
        else
        {
            // Overlapping deletes - compute intersection
            var start1 = op1.Position;
            var end1 = op1.Position + op1.Length;
            var start2 = op2.Position;
            var end2 = op2.Position + op2.Length;
            
            var newStart = Math.Max(start1, start2);
            var newEnd = Math.Min(end1, end2);
            
            if (newStart >= newEnd)
            {
                // No overlap after transformation, operation becomes empty
                return op1 with { Position = Math.Min(start2, start1), Length = 0 };
            }
            
            // Adjust for the portion deleted by op2
            var adjustedStart = start1 < start2 ? start1 : start2;
            var adjustedLength = (end1 > end2 ? end1 - end2 : 0) + (start1 < start2 ? start2 - start1 : 0);
            
            return op1 with { Position = adjustedStart, Length = adjustedLength };
        }
    }
    
    // Transform Delete vs Replace
    private ITextOperation TransformDeleteVsReplace(DeleteOperation op1, ReplaceOperation op2)
    {
        // Convert replace to delete+insert sequence for easier reasoning
        var replaceAsDelete = DeleteOperation.Create(op2.UserId, op2.SelectionStart, op2.SelectionLength);
        var replaceAsInsert = InsertOperation.Create(op2.UserId, op2.SelectionStart, op2.NewContent);
        
        // Transform delete against replace's delete part
        var transformed = (ITextOperation)TransformDeleteVsDelete(op1, replaceAsDelete);
        
        // Then transform against replace's insert part
        if (transformed is DeleteOperation deleteOp)
        {
            transformed = TransformDeleteVsInsert(deleteOp, replaceAsInsert);
        }
        
        return transformed;
    }
    
    // Transform Replace vs Delete
    private ITextOperation TransformReplaceVsDelete(ReplaceOperation op1, DeleteOperation op2)
    {
        // Similar logic but in reverse
        if (op2.Position >= op1.SelectionEnd)
        {
            // Delete is after replace, no change needed
            return op1;
        }
        else if (op2.Position + op2.Length <= op1.SelectionStart)
        {
            // Delete is before replace, adjust selection bounds
            var adjustment = op2.Length;
            return op1 with 
            { 
                SelectionStart = op1.SelectionStart - adjustment,
                SelectionEnd = op1.SelectionEnd - adjustment
            };
        }
        else
        {
            // Delete overlaps with replace selection, adjust selection bounds
            var newStart = Math.Min(op1.SelectionStart, op2.Position);
            var replaceEnd = Math.Max(op1.SelectionEnd, op2.Position + op2.Length);
            var deletedFromReplace = Math.Min(op1.SelectionEnd, op2.Position + op2.Length) - Math.Max(op1.SelectionStart, op2.Position);
            var newEnd = newStart + (op1.SelectionLength - Math.Max(0, deletedFromReplace));
            
            return op1 with 
            { 
                SelectionStart = newStart, 
                SelectionEnd = newEnd
            };
        }
    }
    
    // Transform Replace vs Replace
    private ITextOperation TransformReplaceVsReplace(ReplaceOperation op1, ReplaceOperation op2)
    {
        if (op1.SelectionStart >= op2.SelectionEnd)
        {
            // op1 is after op2, adjust for length difference
            var lengthDiff = op2.NewContent.Length - op2.SelectionLength;
            return op1 with 
            { 
                SelectionStart = op1.SelectionStart + lengthDiff,
                SelectionEnd = op1.SelectionEnd + lengthDiff
            };
        }
        else if (op1.SelectionEnd <= op2.SelectionStart)
        {
            // op1 is before op2, no change needed
            return op1;
        }
        else
        {
            // Overlapping replaces - this is a conflict case
            // For simplicity, convert to insert at the start of the conflict
            var conflictStart = Math.Min(op1.SelectionStart, op2.SelectionStart);
            return InsertOperation.Create(op1.UserId, conflictStart, op1.NewContent, op1.ExpectedSequenceNumber);
        }
    }
    
    // Validation methods
    private OperationValidationResult ValidateInsertOperation(InsertOperation op, string content)
    {
        if (op.Position < 0 || op.Position > content.Length)
            return OperationValidationResult.Invalid("Insert position out of bounds", OperationValidationError.OutOfBounds);
            
        if (string.IsNullOrEmpty(op.Content))
            return OperationValidationResult.Invalid("Insert content cannot be empty", OperationValidationError.MissingContent);
            
        return OperationValidationResult.Valid();
    }
    
    private OperationValidationResult ValidateDeleteOperation(DeleteOperation op, string content)
    {
        if (op.Position < 0 || op.Position >= content.Length)
            return OperationValidationResult.Invalid("Delete position out of bounds", OperationValidationError.OutOfBounds);
            
        if (op.Length <= 0)
            return OperationValidationResult.Invalid("Delete length must be positive", OperationValidationError.InvalidLength);
            
        if (op.Position + op.Length > content.Length)
            return OperationValidationResult.Invalid("Delete range exceeds content length", OperationValidationError.OutOfBounds);
            
        return OperationValidationResult.Valid();
    }
    
    private OperationValidationResult ValidateReplaceOperation(ReplaceOperation op, string content)
    {
        if (op.SelectionStart < 0 || op.SelectionStart >= content.Length)
            return OperationValidationResult.Invalid("Replace selection start out of bounds", OperationValidationError.OutOfBounds);
            
        if (op.SelectionEnd < op.SelectionStart || op.SelectionEnd > content.Length)
            return OperationValidationResult.Invalid("Replace selection end invalid", OperationValidationError.InvalidSelection);
            
        if (op.NewContent == null)
            return OperationValidationResult.Invalid("Replace content cannot be null", OperationValidationError.MissingContent);
            
        return OperationValidationResult.Valid();
    }
}