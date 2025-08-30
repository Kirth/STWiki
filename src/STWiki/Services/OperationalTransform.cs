using STWiki.Models.Collaboration;

namespace STWiki.Services;

public class OperationalTransform
{
    public static TextOperation Transform(TextOperation op1, TextOperation op2)
    {
        // Transform op1 against op2 (op2 was applied first)
        var transformedOp = new TextOperation
        {
            OpType = op1.OpType,
            Position = op1.Position,
            Content = op1.Content,
            Length = op1.Length,
            UserId = op1.UserId,
            Timestamp = op1.Timestamp,
            OperationId = op1.OperationId,
            SelectionStart = op1.SelectionStart,
            SelectionEnd = op1.SelectionEnd
        };
        
        switch (op1.OpType)
        {
            case TextOperation.OperationType.Insert:
                transformedOp.Position = TransformInsertPosition(op1, op2);
                break;
                
            case TextOperation.OperationType.Delete:
                var (newPosition, newLength) = TransformDeleteOperation(op1, op2);
                transformedOp.Position = newPosition;
                transformedOp.Length = newLength;
                break;
                
            case TextOperation.OperationType.Retain:
                // Retain operations don't need transformation in basic implementation
                break;
                
            case TextOperation.OperationType.Replace:
                var (replacePos, replaceStart, replaceEnd) = TransformReplaceOperation(op1, op2);
                transformedOp.Position = replacePos;
                transformedOp.SelectionStart = replaceStart;
                transformedOp.SelectionEnd = replaceEnd;
                Console.WriteLine($"🔄 Transformed replace op: {op1.SelectionStart}-{op1.SelectionEnd} -> {replaceStart}-{replaceEnd} (content: '{op1.Content}')");
                break;
        }
        
        return transformedOp;
    }
    
    public static List<TextOperation> TransformAgainstHistory(TextOperation operation, List<TextOperation> history)
    {
        var transformedOps = new List<TextOperation> { operation };
        
        foreach (var historyOp in history.Where(h => h.Timestamp > operation.Timestamp))
        {
            var newTransformedOps = new List<TextOperation>();
            
            foreach (var op in transformedOps)
            {
                var transformed = Transform(op, historyOp);
                
                // Filter out operations that become invalid after transformation
                if (IsValidOperation(transformed))
                {
                    newTransformedOps.Add(transformed);
                }
            }
            
            transformedOps = newTransformedOps;
        }
        
        return transformedOps;
    }
    
    private static int TransformInsertPosition(TextOperation insertOp, TextOperation otherOp)
    {
        switch (otherOp.OpType)
        {
            case TextOperation.OperationType.Insert:
                // If other insert is at same position or before, shift our insert right
                if (otherOp.Position <= insertOp.Position)
                {
                    return insertOp.Position + otherOp.Length;
                }
                break;
                
            case TextOperation.OperationType.Delete:
                // If delete is before our insert, shift our insert left
                if (otherOp.Position < insertOp.Position)
                {
                    var deletedBeforeInsert = Math.Min(otherOp.Length, insertOp.Position - otherOp.Position);
                    return Math.Max(otherOp.Position, insertOp.Position - deletedBeforeInsert);
                }
                break;
        }
        
        return insertOp.Position;
    }
    
    private static (int position, int length) TransformDeleteOperation(TextOperation deleteOp, TextOperation otherOp)
    {
        switch (otherOp.OpType)
        {
            case TextOperation.OperationType.Insert:
                // If insert is before our delete, shift delete position right
                if (otherOp.Position <= deleteOp.Position)
                {
                    return (deleteOp.Position + otherOp.Length, deleteOp.Length);
                }
                // If insert is within our delete range, increase delete length
                else if (otherOp.Position < deleteOp.Position + deleteOp.Length)
                {
                    return (deleteOp.Position, deleteOp.Length + otherOp.Length);
                }
                break;
                
            case TextOperation.OperationType.Delete:
                // If other delete is before ours, shift our delete left
                if (otherOp.Position < deleteOp.Position)
                {
                    var overlap = Math.Max(0, Math.Min(otherOp.Position + otherOp.Length, deleteOp.Position) - otherOp.Position);
                    var newPosition = Math.Max(otherOp.Position, deleteOp.Position - otherOp.Length);
                    var newLength = deleteOp.Length - overlap;
                    return (newPosition, Math.Max(0, newLength));
                }
                // If other delete overlaps with ours, adjust our delete
                else if (otherOp.Position < deleteOp.Position + deleteOp.Length)
                {
                    var overlap = Math.Min(deleteOp.Position + deleteOp.Length, otherOp.Position + otherOp.Length) - otherOp.Position;
                    var newLength = deleteOp.Length - overlap;
                    return (deleteOp.Position, Math.Max(0, newLength));
                }
                break;
        }
        
        return (deleteOp.Position, deleteOp.Length);
    }
    
    private static (int position, int selectionStart, int selectionEnd) TransformReplaceOperation(TextOperation replaceOp, TextOperation otherOp)
    {
        var newPosition = replaceOp.Position;
        var newSelectionStart = replaceOp.SelectionStart;
        var newSelectionEnd = replaceOp.SelectionEnd;
        
        switch (otherOp.OpType)
        {
            case TextOperation.OperationType.Insert:
                // If insert is before our selection, shift selection right
                if (otherOp.Position <= replaceOp.SelectionStart)
                {
                    newPosition += otherOp.Length;
                    newSelectionStart += otherOp.Length;
                    newSelectionEnd += otherOp.Length;
                }
                // If insert is within our selection, expand selection end
                else if (otherOp.Position < replaceOp.SelectionEnd)
                {
                    newSelectionEnd += otherOp.Length;
                }
                break;
                
            case TextOperation.OperationType.Delete:
                // If delete is entirely before our selection, shift selection left
                if (otherOp.Position + otherOp.Length <= replaceOp.SelectionStart)
                {
                    var shift = otherOp.Length;
                    newPosition = Math.Max(otherOp.Position, newPosition - shift);
                    newSelectionStart = Math.Max(otherOp.Position, newSelectionStart - shift);
                    newSelectionEnd = Math.Max(otherOp.Position, newSelectionEnd - shift);
                }
                // If delete overlaps with our selection, adjust selection bounds
                else if (otherOp.Position < replaceOp.SelectionEnd)
                {
                    // Calculate overlap
                    var overlapStart = Math.Max(otherOp.Position, replaceOp.SelectionStart);
                    var overlapEnd = Math.Min(otherOp.Position + otherOp.Length, replaceOp.SelectionEnd);
                    var overlapLength = Math.Max(0, overlapEnd - overlapStart);
                    
                    if (otherOp.Position <= replaceOp.SelectionStart)
                    {
                        // Delete starts before selection, shift selection left
                        var shift = Math.Min(otherOp.Length, replaceOp.SelectionStart - otherOp.Position);
                        newPosition = Math.Max(otherOp.Position, newPosition - shift);
                        newSelectionStart = Math.Max(otherOp.Position, newSelectionStart - shift);
                        newSelectionEnd = Math.Max(otherOp.Position, newSelectionEnd - otherOp.Length);
                    }
                    else
                    {
                        // Delete is within or after selection start
                        newSelectionEnd = Math.Max(newSelectionStart, newSelectionEnd - overlapLength);
                    }
                }
                break;
                
            case TextOperation.OperationType.Replace:
                // Handle concurrent replace operations
                if (otherOp.SelectionEnd <= replaceOp.SelectionStart)
                {
                    // Other replace is entirely before ours, adjust our position
                    var lengthDiff = (otherOp.Content?.Length ?? 0) - (otherOp.SelectionEnd - otherOp.SelectionStart);
                    newPosition += lengthDiff;
                    newSelectionStart += lengthDiff;
                    newSelectionEnd += lengthDiff;
                }
                else if (otherOp.SelectionStart >= replaceOp.SelectionEnd)
                {
                    // Other replace is entirely after ours, no adjustment needed
                }
                else
                {
                    // Overlapping replace operations - this is a conflict
                    // For now, prioritize by timestamp (earlier operation wins)
                    if (otherOp.Timestamp < replaceOp.Timestamp)
                    {
                        // Other operation wins, adjust our operation to work after it
                        var lengthDiff = (otherOp.Content?.Length ?? 0) - (otherOp.SelectionEnd - otherOp.SelectionStart);
                        newPosition = otherOp.Position + (otherOp.Content?.Length ?? 0);
                        newSelectionStart = newPosition;
                        newSelectionEnd = newPosition;
                        // Convert to insert operation since selection is now empty
                    }
                }
                break;
        }
        
        return (newPosition, newSelectionStart, newSelectionEnd);
    }
    
    private static bool IsValidOperation(TextOperation operation)
    {
        return operation.OpType switch
        {
            TextOperation.OperationType.Insert => !string.IsNullOrEmpty(operation.Content),
            TextOperation.OperationType.Delete => operation.Length > 0,
            TextOperation.OperationType.Retain => operation.Length > 0,
            TextOperation.OperationType.Replace => !string.IsNullOrEmpty(operation.Content) && 
                                                   operation.SelectionStart >= 0 && 
                                                   operation.SelectionEnd >= operation.SelectionStart,
            _ => false
        };
    }
}