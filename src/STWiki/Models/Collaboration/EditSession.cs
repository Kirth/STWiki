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
    
    private readonly object _lockObject = new object();
    
    public void AddOperation(TextOperation operation)
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
                    Console.WriteLine($"  Replace range {operation.SelectionStart}-{operation.SelectionEnd} ('{selectedText}') with '{operation.Content}'");
                    
                    CurrentContent = before + (operation.Content ?? "") + after;
                    
                    Console.WriteLine($"  Result: '{CurrentContent}'");
                }
                else
                {
                    Console.WriteLine($"⚠️ Invalid replace operation bounds: {operation.SelectionStart}-{operation.SelectionEnd} for content length {CurrentContent.Length}");
                }
                break;
        }
    }
}