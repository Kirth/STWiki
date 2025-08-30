using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using STWiki.Models.Collaboration;
using STWiki.Services;
using STWiki.Data;
using System.Security.Claims;

namespace STWiki.Hubs;

[Authorize(Policy = "RequireEditor")]
public class EditHub : Hub
{
    private readonly IEditSessionService _editSessionService;
    private readonly AppDbContext _context;
    private readonly ILogger<EditHub> _logger;
    
    public EditHub(IEditSessionService editSessionService, AppDbContext context, ILogger<EditHub> logger)
    {
        _editSessionService = editSessionService;
        _context = context;
        _logger = logger;
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
            await Groups.AddToGroupAsync(Context.ConnectionId, $"edit_{pageId}");
            
            // Send current document state to joining user
            await Clients.Caller.SendAsync("DocumentState", session.CurrentContent, session.OperationCounter);
            
            // Send current user list to joining user
            var connectedUsers = session.ConnectedUsers.Values.ToList();
            await Clients.Caller.SendAsync("UserList", connectedUsers);
            
            // Notify others about new user
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
                // Broadcast the processed operation to all other users in the room
                await Clients.OthersInGroup($"edit_{pageId}").SendAsync("ReceiveOperation", finalOperation);
                
                // Confirm operation back to sender if it's their operation
                if (finalOperation.UserId == operation.UserId && finalOperation.OperationId == operation.OperationId)
                {
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
}