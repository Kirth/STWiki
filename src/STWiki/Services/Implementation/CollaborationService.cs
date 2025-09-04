using STWiki.Services.Interfaces;
using STWiki.Models.Collaboration;
using STWiki.Models.Collaboration.Events;
using STWiki.Models.Collaboration.Operations;

namespace STWiki.Services.Implementation;

/// <summary>
/// Implementation of collaboration service for real-time editing
/// </summary>
public class CollaborationService : ICollaborationService
{
    private readonly ICollaborationSessionService _sessionService;
    private readonly IOperationTransformService _transformService;
    private readonly ISignalRConnectionService _signalRService;
    private readonly ILogger<CollaborationService> _logger;
    
    private Guid _currentPageId;
    private string _currentUserId = string.Empty;
    private bool _isDisposed;
    
    public event EventHandler<OperationReceivedEventArgs>? OperationReceived;
    public event EventHandler<ContentChangedEventArgs>? ContentChanged;
    public event EventHandler<CollaborationStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<UserPresenceChangedEventArgs>? UserPresenceChanged;
    public event EventHandler<CursorPositionChangedEventArgs>? CursorPositionChanged;
    
    public CollaborationStatus Status { get; private set; } = CollaborationStatus.Offline;
    
    public CollaborationService(
        ICollaborationSessionService sessionService,
        IOperationTransformService transformService,
        ISignalRConnectionService signalRService,
        ILogger<CollaborationService> logger)
    {
        _sessionService = sessionService;
        _transformService = transformService;
        _signalRService = signalRService;
        _logger = logger;
        
        // Subscribe to SignalR events
        _signalRService.OperationReceived += OnSignalROperationReceived;
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
    }
    
    public async Task InitializeAsync(Guid pageId, string userId)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CollaborationService));
            
        _logger.LogDebug("Initializing collaboration for page {PageId}, user {UserId}", pageId, userId);
        
        _currentPageId = pageId;
        _currentUserId = userId;
        
        try
        {
            // Connect to SignalR
            await _signalRService.ConnectAsync();
            
            // Join the page group
            await _signalRService.JoinPageGroupAsync(pageId, userId);
            
            // Get or create session
            await _sessionService.GetOrCreateSessionAsync(pageId, userId);
            
            Status = CollaborationStatus.Online;
            _logger.LogDebug("Successfully initialized collaboration for page {PageId}", pageId);
        }
        catch (Exception ex)
        {
            Status = CollaborationStatus.Error;
            _logger.LogError(ex, "Failed to initialize collaboration for page {PageId}", pageId);
            throw;
        }
    }
    
    public async Task SendOperationAsync(ITextOperation operation)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CollaborationService));
            
        if (Status != CollaborationStatus.Online)
            throw new InvalidOperationException("Collaboration service is not connected");
            
        try
        {
            _logger.LogDebug("Sending operation {OperationId} for page {PageId}", 
                operation.OperationId, _currentPageId);
            
            // Process operation through session service
            var result = await _sessionService.ProcessOperationAsync(_currentPageId, operation);
            
            if (result.Success && result.ProcessedOperation != null)
            {
                // Send to other clients via SignalR
                await _signalRService.SendOperationAsync(_currentPageId, result.ProcessedOperation);
                
                // Raise local content changed event
                // TODO: Need to get content differently since GetSessionAsync is not available
                ContentChanged?.Invoke(this, new ContentChangedEventArgs(
                    _currentPageId, "content updated"));
            }
            else
            {
                _logger.LogWarning("Operation processing failed: {Error}", result.ErrorMessage);
                throw new InvalidOperationException($"Operation processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending operation {OperationId}", operation.OperationId);
            throw;
        }
    }
    
    public async Task<CollaborationSession?> GetSessionAsync(Guid pageId)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CollaborationService));
            
        // TODO: The interface doesn't have GetSessionAsync method
        // This needs to be implemented differently
        return null;
    }
    
    public async Task<IEnumerable<UserPresence>> GetConnectedUsersAsync(Guid pageId)
    {
        // TODO: Need to implement using available interface methods
        await Task.CompletedTask;
        return Enumerable.Empty<UserPresence>();
    }
    
    public async Task UpdateCursorPositionAsync(CursorPosition cursor)
    {
        await _sessionService.UpdateCursorAsync(_currentPageId, cursor.UserId, cursor);
    }
    
    private async void OnSignalROperationReceived(object? sender, OperationReceivedEventArgs e)
    {
        if (e.Operation.UserId == _currentUserId)
            return; // Ignore our own operations
            
        try
        {
            _logger.LogDebug("Received remote operation {OperationId} from user {UserId}", 
                e.Operation.OperationId, e.Operation.UserId);
            
            // Process the remote operation
            var result = await _sessionService.ProcessOperationAsync(_currentPageId, e.Operation);
            
            if (result.Success)
            {
                // Forward to subscribers
                OperationReceived?.Invoke(this, e);
                
                // Raise content changed event
                // TODO: Need to get content differently since GetSessionAsync is not available
                ContentChanged?.Invoke(this, new ContentChangedEventArgs(
                    _currentPageId, "content updated"));
            }
            else
            {
                _logger.LogWarning("Failed to process remote operation {OperationId}: {Error}", 
                    e.Operation.OperationId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing remote operation {OperationId}", e.Operation.OperationId);
        }
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Status = e.IsConnected ? CollaborationStatus.Online : CollaborationStatus.Offline;
        _logger.LogDebug("Collaboration connection state changed: {Status}", Status);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;
            
        _logger.LogDebug("Disposing collaboration service for page {PageId}", _currentPageId);
        
        try
        {
            if (_currentPageId != Guid.Empty && !string.IsNullOrEmpty(_currentUserId))
            {
                await _signalRService.LeavePageGroupAsync(_currentPageId, _currentUserId);
            }
            
            await _signalRService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during collaboration service disposal");
        }
        finally
        {
            _isDisposed = true;
            Status = CollaborationStatus.Offline;
        }
    }
}