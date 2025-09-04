namespace STWiki.Models.Collaboration.Operations;

/// <summary>
/// Enumeration of text operation types
/// </summary>
public enum TextOperationType
{
    /// <summary>
    /// Insert text at a specific position
    /// </summary>
    Insert,
    
    /// <summary>
    /// Delete text from a specific position
    /// </summary>
    Delete,
    
    /// <summary>
    /// Replace selected text with new content
    /// </summary>
    Replace,
    
    /// <summary>
    /// Retain characters (used in some OT algorithms)
    /// </summary>
    Retain
}