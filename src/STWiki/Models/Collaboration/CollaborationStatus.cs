namespace STWiki.Models.Collaboration;

/// <summary>
/// Enumeration of collaboration connection states
/// </summary>
public enum CollaborationStatus
{
    /// <summary>
    /// Not connected to collaboration service
    /// </summary>
    Offline,
    
    /// <summary>
    /// Attempting to connect to collaboration service
    /// </summary>
    Connecting,
    
    /// <summary>
    /// Connected and ready for collaboration
    /// </summary>
    Online,
    
    /// <summary>
    /// Connected but attempting to reconnect due to issues
    /// </summary>
    Reconnecting,
    
    /// <summary>
    /// Connection failed with error
    /// </summary>
    Error,
    
    /// <summary>
    /// Disconnecting from collaboration service
    /// </summary>
    Disconnecting
}