using Microsoft.AspNetCore.SignalR.Client;
using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration.Events;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of SignalR connection service for collaboration
/// </summary>
public class SignalRConnectionService : ISignalRConnectionService, IAsyncDisposable
{
    private readonly ILogger<SignalRConnectionService> _logger;
    private HubConnection? _connection;
    private bool _isDisposed;
    
    public event EventHandler<OperationReceivedEventArgs>? OperationReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    
    public SignalRConnectionService(ILogger<SignalRConnectionService> logger)
    {
        _logger = logger;
    }
    
    public async Task ConnectAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SignalRConnectionService));
            
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        
        _connection = new HubConnectionBuilder()
            .WithUrl("/hubs/edit") // TODO: Make this configurable
            .WithAutomaticReconnect()
            .Build();
            
        // Set up event handlers
        _connection.Closed += OnConnectionClosed;
        _connection.Reconnected += OnReconnected;
        _connection.Reconnecting += OnReconnecting;
        
        // Set up operation received handler
        _connection.On<Guid, string>("ReceiveOperation", OnOperationReceived);
        _connection.On<Guid, string, int, int>("ReceiveCursorUpdate", OnCursorUpdateReceived);
        
        try
        {
            await _connection.StartAsync();
            _logger.LogDebug("SignalR connection established");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, ex.Message));
            throw;
        }
    }
    
    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
            
            _logger.LogDebug("SignalR connection closed");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false));
        }
    }
    
    public async Task JoinPageGroupAsync(Guid pageId, string userId)
    {
        if (_connection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException("SignalR connection is not active");
            
        try
        {
            await _connection.InvokeAsync("JoinPageGroup", pageId.ToString(), userId);
            _logger.LogDebug("Joined page group for page {PageId}, user {UserId}", pageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join page group for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task LeavePageGroupAsync(Guid pageId, string userId)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return; // Connection already closed
            
        try
        {
            await _connection.InvokeAsync("LeavePageGroup", pageId.ToString(), userId);
            _logger.LogDebug("Left page group for page {PageId}, user {UserId}", pageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave page group for page {PageId}", pageId);
            // Don't throw on leave - connection might be closing
        }
    }
    
    public async Task SendOperationAsync(Guid pageId, ITextOperation operation)
    {
        if (_connection?.State != HubConnectionState.Connected)
            throw new InvalidOperationException("SignalR connection is not active");
            
        try
        {
            // Serialize the operation to JSON for transmission
            var operationJson = System.Text.Json.JsonSerializer.Serialize(operation, operation.GetType());
            await _connection.InvokeAsync("SendOperation", pageId.ToString(), operationJson);
            
            _logger.LogDebug("Sent operation {OperationId} for page {PageId}", operation.OperationId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send operation {OperationId} for page {PageId}", 
                operation.OperationId, pageId);
            throw;
        }
    }
    
    public async Task SendCursorUpdateAsync(Guid pageId, string userId, int position, int selectionEnd)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return; // Don't throw for cursor updates
            
        try
        {
            await _connection.InvokeAsync("SendCursorUpdate", pageId.ToString(), userId, position, selectionEnd);
            _logger.LogDebug("Sent cursor update for page {PageId}, user {UserId}: {Position}-{SelectionEnd}", 
                pageId, userId, position, selectionEnd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cursor update for page {PageId}, user {UserId}", 
                pageId, userId);
            // Don't throw for cursor updates
        }
    }
    
    private async void OnOperationReceived(Guid pageId, string operationJson)
    {
        try
        {
            // TODO: Deserialize operation based on type information in JSON
            // For now, this is a simplified version
            _logger.LogDebug("Received operation for page {PageId}: {OperationJson}", pageId, operationJson);
            
            // This would need proper deserialization logic
            // var operation = DeserializeOperation(operationJson);
            // OperationReceived?.Invoke(this, new OperationReceivedEventArgs(pageId, operation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received operation for page {PageId}", pageId);
        }
    }
    
    private void OnCursorUpdateReceived(Guid pageId, string userId, int position, int selectionEnd)
    {
        _logger.LogDebug("Received cursor update for page {PageId}, user {UserId}: {Position}-{SelectionEnd}", 
            pageId, userId, position, selectionEnd);
        
        // TODO: Forward to cursor update handlers
    }
    
    private Task OnConnectionClosed(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR connection closed");
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(false, exception?.Message));
        return Task.CompletedTask;
    }
    
    private Task OnReconnected(string? connectionId)
    {
        _logger.LogDebug("SignalR connection reconnected: {ConnectionId}", connectionId);
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(true));
        return Task.CompletedTask;
    }
    
    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogDebug(exception, "SignalR connection reconnecting");
        return Task.CompletedTask;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
            
        await DisconnectAsync();
        _isDisposed = true;
    }
}