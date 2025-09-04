using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using STWiki.Models.Collaboration;
using STWiki.Services;
using STWiki.Services.Interfaces;
using STWiki.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace STWiki.Hubs;

[Authorize(Policy = "RequireEditor")]
public class EditHub : Hub
{
    private readonly IEditSessionService _editSessionService;
    private readonly AppDbContext _context;
    private readonly ILogger<EditHub> _logger;
    
    // New refactored services (optional - for testing side-by-side)
    private readonly ICollaborationService? _collaborationService;
    private readonly ICollaborationSessionService? _collaborationSessionService;
    
    public EditHub(IEditSessionService editSessionService, AppDbContext context, ILogger<EditHub> logger,
        ICollaborationService? collaborationService = null,
        ICollaborationSessionService? collaborationSessionService = null)
    {
        _editSessionService = editSessionService;
        _context = context;
        _logger = logger;
        _collaborationService = collaborationService;
        _collaborationSessionService = collaborationSessionService;
    }
    
    public async Task JoinEditRoom(string pageId, string userId)
    {
        try
        {
            // Verify user has permission to edit this page
            var pageGuid = Guid.Parse(pageId);
            var page = await _context.Pages.FindAsync(pageGuid);
            if (page == null)
            {
                await Clients.Caller.SendAsync("Error", "Page not found");
                return;
            }
            
            // Get user info
            var userDisplayName = Context.User?.FindFirst("name")?.Value ?? 
                                 Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? 
                                 "Anonymous User";
            var userEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value ?? "";
            
            // Get or create edit session
            var session = await _editSessionService.GetOrCreateSessionAsync(pageId, page.Body ?? "");
            
            // Add user to session
            var userState = await _editSessionService.AddUserToSessionAsync(
                pageId, userId, userDisplayName, userEmail, Context.ConnectionId);
            
            // Join SignalR group
            _logger.LogInformation("👥 Adding connection {ConnectionId} to group edit_{PageId}", Context.ConnectionId, pageId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"edit_{pageId}");
            _logger.LogInformation("✅ Successfully added to group edit_{PageId}", pageId);
            
            // Send current document state to joining user
            _logger.LogInformation("📄 Sending DocumentState to joining user {UserId}", userId);
            await Clients.Caller.SendAsync("DocumentState", session.CurrentContent, session.OperationCounter);
            
            // Send current user list to joining user
            var connectedUsers = session.ConnectedUsers.Values.ToList();
            _logger.LogInformation("👥 Sending UserList with {UserCount} users to {UserId}", connectedUsers.Count, userId);
            await Clients.Caller.SendAsync("UserList", connectedUsers);
            
            // Notify others about new user
            _logger.LogInformation("📢 Notifying others in group edit_{PageId} about new user {UserId}", pageId, userId);
            await Clients.OthersInGroup($"edit_{pageId}").SendAsync("UserJoined", userState);
            
            _logger.LogInformation("User {UserId} joined edit room for page {PageId}", userId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining edit room {PageId} for user {UserId}", pageId, userId);
            await Clients.Caller.SendAsync("Error", "Failed to join edit room");
        }
    }
    
    public async Task LeaveEditRoom(string pageId, string userId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"edit_{pageId}");
            await _editSessionService.RemoveUserFromSessionAsync(pageId, userId);
            
            // Notify others about user leaving
            await Clients.OthersInGroup($"edit_{pageId}").SendAsync("UserLeft", userId);
            
            _logger.LogInformation("User {UserId} left edit room for page {PageId}", userId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving edit room {PageId} for user {UserId}", pageId, userId);
        }
    }
    
    public async Task SendTextOperation(string pageId, TextOperation operation)
    {
        try
        {
            // Queue operation for sequential processing to prevent race conditions
            var processedOperations = await _editSessionService.QueueAndProcessOperationAsync(pageId, operation);
            
            foreach (var finalOperation in processedOperations)
            {
                // Get session to check connected users for debugging
                var session = await _editSessionService.GetSessionAsync(pageId);
                var connectedCount = session?.ConnectedUsers.Count ?? 0;
                
                // Broadcast the processed operation to all other users in the room
                _logger.LogInformation("🚀 SENDING ReceiveOperation to others in group edit_{PageId}: {OpType} at {Position} by {UserId} - {ConnectedCount} users in session", 
                    pageId, finalOperation.OpType, finalOperation.Position, finalOperation.UserId, connectedCount);
                    
                await Clients.OthersInGroup($"edit_{pageId}").SendAsync("ReceiveOperation", finalOperation);
                _logger.LogInformation("✅ Successfully sent ReceiveOperation to others in group edit_{PageId}", pageId);
                
                // Confirm operation back to sender if it's their operation
                if (finalOperation.UserId == operation.UserId && finalOperation.OperationId == operation.OperationId)
                {
                    _logger.LogInformation("📤 Sending OperationConfirmed to caller for operation {OpId}", finalOperation.OperationId);
                    await Clients.Caller.SendAsync("OperationConfirmed", finalOperation.OperationId, finalOperation.ServerSequenceNumber);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing text operation for page {PageId}", pageId);
            await Clients.Caller.SendAsync("OperationRejected", operation.OperationId, "Server error");
        }
    }
    
    public async Task SendCursorUpdate(string pageId, CursorPosition cursor)
    {
        try
        {
            await _editSessionService.UpdateUserCursorAsync(pageId, cursor.UserId, cursor);
            
            // Broadcast cursor update to others
            await Clients.OthersInGroup($"edit_{pageId}").SendAsync("ReceiveCursor", cursor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cursor for page {PageId}", pageId);
        }
    }
    
    public async Task RequestDocumentSync(string pageId)
    {
        try
        {
            var session = await _editSessionService.GetSessionAsync(pageId);
            if (session != null)
            {
                await Clients.Caller.SendAsync("DocumentState", session.CurrentContent, session.OperationCounter, session.GlobalSequenceNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing document for page {PageId}", pageId);
        }
    }
    
    /// <summary>
    /// Update client's last known sequence number for operation history tracking
    /// </summary>
    public async Task UpdateClientState(string pageId, long sequenceNumber)
    {
        try
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                        Context.User?.FindFirst("sub")?.Value ?? "";
            
            if (!string.IsNullOrEmpty(userId))
            {
                var session = await _editSessionService.GetSessionAsync(pageId);
                session?.UpdateClientState(userId, sequenceNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client state for page {PageId}", pageId);
        }
    }
    
    /// <summary>
    /// Get operations since client's last known state
    /// </summary>
    public async Task RequestOperationsSince(string pageId, long clientSequenceNumber)
    {
        try
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                        Context.User?.FindFirst("sub")?.Value ?? "";
            
            if (!string.IsNullOrEmpty(userId))
            {
                var session = await _editSessionService.GetSessionAsync(pageId);
                if (session != null)
                {
                    var operations = session.GetOperationsSinceClientState(userId, clientSequenceNumber);
                    await Clients.Caller.SendAsync("OperationsSinceState", operations);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operations since state for page {PageId}", pageId);
        }
    }
    
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Find any edit rooms this connection was part of and remove the user
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                var sessions = _editSessionService.GetAllSessions();
                foreach (var session in sessions)
                {
                    if (session.ConnectedUsers.TryGetValue(userId, out var user) && 
                        user.ConnectionId == Context.ConnectionId)
                    {
                        await _editSessionService.RemoveUserFromSessionAsync(session.PageId, userId);
                        await Clients.Group($"edit_{session.PageId}").SendAsync("UserLeft", userId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during client disconnect: {ConnectionId}", Context.ConnectionId);
        }
        
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Phase 1: Enhanced State Reconciliation - Server-side state verification
    /// Compares client state with server state and triggers resync if needed
    /// </summary>
    public async Task RequestStateSync(string pageId, long clientSequenceNumber, string clientContentHash)
    {
        try
        {
            _logger.LogInformation("📥 State sync request received for page {PageId} - Client seq: {ClientSeq}, hash: {ClientHash}", 
                pageId, clientSequenceNumber, clientContentHash[..8] + "...");
                
            var session = await _editSessionService.GetSessionAsync(pageId);
            if (session == null)
            {
                _logger.LogWarning("❌ Edit session not found for page {PageId}", pageId);
                await Clients.Caller.SendAsync("Error", "Edit session not found");
                return;
            }

            var serverContentHash = ComputeContentHash(session.CurrentContent);
            var isInSync = clientSequenceNumber == session.GlobalSequenceNumber && 
                          clientContentHash == serverContentHash;

            _logger.LogInformation("🔍 State comparison for page {PageId}: Server seq: {ServerSeq}, hash: {ServerHash}, Content length: {ContentLength}, InSync: {InSync}", 
                pageId, session.GlobalSequenceNumber, serverContentHash[..8] + "...", session.CurrentContent?.Length, isInSync);

            if (isInSync)
            {
                await Clients.Caller.SendAsync("StateVerified", session.GlobalSequenceNumber);
                _logger.LogInformation("✅ State verified for page {PageId} at sequence {Seq}", pageId, session.GlobalSequenceNumber);
            }
            else
            {
                // Send full resync
                await Clients.Caller.SendAsync("RequiredResync", 
                    session.CurrentContent, 
                    session.GlobalSequenceNumber,
                    serverContentHash);
                _logger.LogWarning("🚨 State resync required for page {PageId}. Client seq: {ClientSeq}, Server seq: {ServerSeq}, Client hash: {ClientHash}, Server hash: {ServerHash}", 
                    pageId, clientSequenceNumber, session.GlobalSequenceNumber, clientContentHash[..8] + "...", serverContentHash[..8] + "...");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during state sync for page {PageId}", pageId);
            await Clients.Caller.SendAsync("Error", "State sync failed");
        }
    }
    
    /// <summary>
    /// Compute SHA-256 hash of content for state verification
    /// </summary>
    private static string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content ?? "");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
    
    // ===== NEW REFACTORED ARCHITECTURE METHODS =====
    
    /// <summary>
    /// Join edit room using the new refactored collaboration services
    /// </summary>
    public async Task JoinEditRoomV2(string pageId, string userId)
    {
        if (_collaborationService == null)
        {
            await Clients.Caller.SendAsync("Error", "Refactored collaboration services not available");
            return;
        }
        
        try
        {
            _logger.LogInformation("🚀 [V2] User {UserId} joining edit room for page {PageId}", userId, pageId);
            
            // Initialize collaboration for this page and user
            await _collaborationService.InitializeAsync(Guid.Parse(pageId), userId);
            
            // Join SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"edit_{pageId}_v2");
            
            // Get session information
            var session = await _collaborationService.GetSessionAsync(Guid.Parse(pageId));
            if (session != null)
            {
                // Send current state to joining user
                await Clients.Caller.SendAsync("DocumentStateV2", session.CurrentContent, session.CurrentSequenceNumber);
                
                // Send connected users
                var connectedUsers = await _collaborationService.GetConnectedUsersAsync(Guid.Parse(pageId));
                await Clients.Caller.SendAsync("UserListV2", connectedUsers);
                
                // Notify others about new user
                await Clients.OthersInGroup($"edit_{pageId}_v2").SendAsync("UserJoinedV2", userId);
            }
            
            _logger.LogInformation("✅ [V2] User {UserId} successfully joined edit room for page {PageId}", userId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [V2] Error joining edit room {PageId} for user {UserId}", pageId, userId);
            await Clients.Caller.SendAsync("Error", $"[V2] Failed to join edit room: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Leave edit room using the new refactored collaboration services
    /// </summary>
    public async Task LeaveEditRoomV2(string pageId, string userId)
    {
        if (_collaborationService == null) return;
        
        try
        {
            _logger.LogInformation("👋 [V2] User {UserId} leaving edit room for page {PageId}", userId, pageId);
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"edit_{pageId}_v2");
            await Clients.OthersInGroup($"edit_{pageId}_v2").SendAsync("UserLeftV2", userId);
            
            // Dispose collaboration service for this user
            await _collaborationService.DisposeAsync();
            
            _logger.LogInformation("✅ [V2] User {UserId} left edit room for page {PageId}", userId, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [V2] Error leaving edit room {PageId} for user {UserId}", pageId, userId);
        }
    }
    
    /// <summary>
    /// Send text operation using the new refactored services  
    /// </summary>
    public async Task SendTextOperationV2(string pageId, string operationJson)
    {
        if (_collaborationService == null)
        {
            await Clients.Caller.SendAsync("OperationRejectedV2", "unknown", "Refactored services not available");
            return;
        }
        
        try
        {
            _logger.LogInformation("📝 [V2] Received operation for page {PageId}: {OperationJson}", pageId, operationJson);
            
            // For now, just echo back to test the pipeline
            // TODO: Deserialize operation JSON and process through new services
            
            await Clients.OthersInGroup($"edit_{pageId}_v2").SendAsync("ReceiveOperationV2", operationJson);
            await Clients.Caller.SendAsync("OperationConfirmedV2", "temp-id", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            
            _logger.LogInformation("✅ [V2] Operation processed for page {PageId}", pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [V2] Error processing operation for page {PageId}", pageId);
            await Clients.Caller.SendAsync("OperationRejectedV2", "unknown", ex.Message);
        }
    }
}