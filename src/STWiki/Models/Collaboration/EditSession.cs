using System.Collections.Concurrent;

namespace STWiki.Models.Collaboration;

public class EditSession
{
    public string PageId { get; set; } = string.Empty;
    public string CurrentContent { get; set; } = string.Empty;
    public List<TextOperation> OperationHistory { get; set; } = new();
    public ConcurrentDictionary<string, UserState> ConnectedUsers { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int OperationCounter { get; set; } = 0;
    
    // Enhanced sequencing for race condition prevention
    public long GlobalSequenceNumber { get; set; } = 0;
    public readonly object _operationLock = new object();
    public readonly Queue<TextOperation> _operationQueue = new Queue<TextOperation>();
    public readonly Dictionary<string, long> _clientStates = new Dictionary<string, long>();
    
    private readonly object _lockObject = new object();
    
    /// <summary>
    /// Queue operation for sequential processing to prevent race conditions
    /// </summary>
    public void QueueOperation(TextOperation operation)
    {
        lock (_operationLock)
        {
            _operationQueue.Enqueue(operation);
        }
    }
    
    /// <summary>
    /// Process queued operations with strict sequencing
    /// </summary>
    public List<TextOperation> ProcessQueuedOperations()
    {
        var processedOperations = new List<TextOperation>();
        
        lock (_operationLock)
        {
            while (_operationQueue.Count > 0)
            {
                var operation = _operationQueue.Dequeue();
                
                // Assign server-side sequence number for strict ordering
                operation.ServerSequenceNumber = ++GlobalSequenceNumber;
                
                // Add to history and apply
                AddOperationInternal(operation);
                processedOperations.Add(operation);
            }
        }
        
        return processedOperations;
    }
    
    /// <summary>
    /// Update client state tracking for operation history management
    /// </summary>
    public void UpdateClientState(string userId, long sequenceNumber)
    {
        lock (_operationLock)
        {
            _clientStates[userId] = sequenceNumber;
        }
    }
    
    /// <summary>
    /// Get operations that a client hasn't seen yet
    /// </summary>
    public List<TextOperation> GetOperationsSinceClientState(string userId, long clientSequenceNumber)
    {
        lock (_lockObject)
        {
            return OperationHistory
                .Where(op => op.ServerSequenceNumber > clientSequenceNumber)
                .OrderBy(op => op.ServerSequenceNumber)
                .ToList();
        }
    }
    
    public void AddOperation(TextOperation operation)
    {
        AddOperationInternal(operation);
    }
    
    private void AddOperationInternal(TextOperation operation)
    {
        lock (_lockObject)
        {
            OperationHistory.Add(operation);
            OperationCounter++;
            LastActivity = DateTime.UtcNow;
            
            // Apply operation to current content
            ApplyOperation(operation);
            
            // Keep only last 1000 operations for performance
            if (OperationHistory.Count > 1000)
            {
                OperationHistory.RemoveRange(0, OperationHistory.Count - 1000);
            }
        }
    }
    
    public void AddUser(UserState user)
    {
        ConnectedUsers.TryAdd(user.UserId, user);
        LastActivity = DateTime.UtcNow;
    }
    
    public void RemoveUser(string userId)
    {
        ConnectedUsers.TryRemove(userId, out _);
        LastActivity = DateTime.UtcNow;
    }
    
    public void UpdateUserCursor(string userId, CursorPosition cursor)
    {
        if (ConnectedUsers.TryGetValue(userId, out var user))
        {
            user.Cursor = cursor;
            user.LastSeen = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
        }
    }
    
    public bool IsIdle => DateTime.UtcNow.Subtract(LastActivity).TotalMinutes > 30;
    
    public int ActiveUserCount => ConnectedUsers.Values.Count(u => u.IsActive);
    
    private void ApplyOperation(TextOperation operation)
    {
        switch (operation.OpType)
        {
            case TextOperation.OperationType.Insert:
                if (operation.Content != null && operation.Position <= CurrentContent.Length)
                {
                    CurrentContent = CurrentContent.Insert(operation.Position, operation.Content);
                }
                break;
                
            case TextOperation.OperationType.Delete:
                if (operation.Position < CurrentContent.Length)
                {
                    var deleteLength = Math.Min(operation.Length, CurrentContent.Length - operation.Position);
                    CurrentContent = CurrentContent.Remove(operation.Position, deleteLength);
                }
                break;
                
            case TextOperation.OperationType.Replace:
                if (operation.SelectionStart <= CurrentContent.Length && 
                    operation.SelectionEnd <= CurrentContent.Length && 
                    operation.SelectionStart <= operation.SelectionEnd)
                {
                    var before = CurrentContent.Substring(0, operation.SelectionStart);
                    var after = CurrentContent.Substring(operation.SelectionEnd);
                    var selectedText = CurrentContent.Substring(operation.SelectionStart, operation.SelectionEnd - operation.SelectionStart);
                    
                    Console.WriteLine($"📋 EditSession Apply Replace:");
                    Console.WriteLine($"  Current: '{CurrentContent}'");
                    Console.WriteLine($"  Current length: {CurrentContent.Length}");
                    Console.WriteLine($"  Replace range {operation.SelectionStart}-{operation.SelectionEnd} ('{selectedText}') with '{operation.Content}'");
                    Console.WriteLine($"  operation.Content length: {operation.Content?.Length ?? 0}");
                    
                    CurrentContent = before + (operation.Content ?? "") + after;
                    
                    Console.WriteLine($"  Result: '{CurrentContent}'");
                    Console.WriteLine($"  Result length: {CurrentContent.Length}");
                }
                else
                {
                    Console.WriteLine($"⚠️ Invalid replace operation bounds: {operation.SelectionStart}-{operation.SelectionEnd} for content length {CurrentContent.Length}");
                }
                break;
        }
    }
}