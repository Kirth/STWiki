using STWiki.Models.Collaboration.Events;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Interfaces;

/// <summary>
/// Service interface for managing SignalR connections for collaboration
/// </summary>
public interface ISignalRConnectionService
{
    /// <summary>
    /// Event fired when an operation is received from SignalR
    /// </summary>
    event EventHandler<OperationReceivedEventArgs> OperationReceived;
    
    /// <summary>
    /// Event fired when connection state changes
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
    
    /// <summary>
    /// Whether the SignalR connection is currently active
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Connect to the SignalR hub
    /// </summary>
    Task ConnectAsync();
    
    /// <summary>
    /// Disconnect from the SignalR hub
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Join a page collaboration group
    /// </summary>
    Task JoinPageGroupAsync(Guid pageId, string userId);
    
    /// <summary>
    /// Leave a page collaboration group
    /// </summary>
    Task LeavePageGroupAsync(Guid pageId, string userId);
    
    /// <summary>
    /// Send an operation to other clients via SignalR
    /// </summary>
    Task SendOperationAsync(Guid pageId, ITextOperation operation);
    
    /// <summary>
    /// Send cursor position update to other clients
    /// </summary>
    Task SendCursorUpdateAsync(Guid pageId, string userId, int position, int selectionEnd);
}

/// <summary>
/// Event args for connection state changes
/// </summary>
public record ConnectionStateChangedEventArgs(bool IsConnected, string? ErrorMessage = null);