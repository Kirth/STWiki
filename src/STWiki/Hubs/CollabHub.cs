using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using STWiki.Services;

namespace STWiki.Hubs;

[Authorize(Policy = "RequireEditor")]
public class CollabHub : Hub
{
    private readonly ICollabStore _store;
    private readonly ILogger<CollabHub> _logger;

    public CollabHub(ICollabStore store, ILogger<CollabHub> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task Init(Guid pageId, string clientVectorJson, Guid clientId)
    {
        try
        {
            await _store.EnsureCanEdit(Context.User!, pageId);
            var session = await _store.GetOrCreateActiveSession(pageId);
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(session.Id));
            var init = await _store.GetInitPayload(session.Id, clientVectorJson);
            await Clients.Caller.SendAsync("Init", init);

            _logger.LogDebug("Client {ClientId} initialized for session {SessionId}", clientId, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize client {ClientId} for page {PageId}", clientId, pageId);
            await Clients.Caller.SendAsync("Error", "Failed to initialize collaboration session");
        }
    }

    public async Task Push(Guid pageId, string updateData, string clientVectorJson, Guid clientId)
    {
        _logger.LogInformation("🚀 PUSH METHOD ENTRY - Client: {ClientId}, Page: {PageId}, User: {User}, Connection: {ConnectionId}", 
            clientId, pageId, Context.User?.Identity?.Name ?? "null", Context.ConnectionId);
            
        try
        {
            _logger.LogInformation("📥 Received Push request from client {ClientId} for page {PageId}, update data: {Data}", 
                clientId, pageId, updateData);
            
            _logger.LogInformation("🔧 About to check authorization...");
                
            // Check authorization first
            _logger.LogInformation("🔒 Checking authorization for user {User} on page {PageId}", 
                Context.User?.Identity?.Name ?? "null", pageId);
                
            await _store.EnsureCanEdit(Context.User!, pageId);
            _logger.LogInformation("✅ Authorization passed for page {PageId}", pageId);
            
            _logger.LogInformation("🔧 About to get session...");
                
            var session = await _store.RequireActiveSession(pageId);
            _logger.LogInformation("📋 Found session {SessionId} for page {PageId}", session.Id, pageId);
            
            var updateBytes = System.Text.Encoding.UTF8.GetBytes(updateData);
            var seq = await _store.AppendUpdate(session.Id, clientId, updateBytes, clientVectorJson);
            _logger.LogInformation("💾 Update appended with sequence {Seq}", seq);
            
            await Clients.OthersInGroup(Group(session.Id))
                .SendAsync("Update", updateData, seq);
            _logger.LogInformation("📤 Broadcasted update {Seq} to group {Group}", seq, Group(session.Id));

            _logger.LogInformation("📤 Broadcasted update {Seq} from client {ClientId} in session {SessionId}", 
                seq, clientId, session.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PUSH METHOD ERROR - Client: {ClientId}, Page: {PageId}, Error: {Message}", 
                clientId, pageId, ex.Message);
            _logger.LogError(ex, "❌ Full exception details: {Exception}", ex.ToString());
            await Clients.Caller.SendAsync("Error", "Failed to push update");
        }
    }

    public async Task Presence(Guid pageId, string presenceJson)
    {
        try
        {
            var session = await _store.RequireActiveSession(pageId);
            await _store.BroadcastPresence(pageId, presenceJson, Group(session.Id), 
                groupName => Clients.OthersInGroup(groupName));

            _logger.LogDebug("Broadcasted presence for page {PageId}", pageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast presence for page {PageId}", pageId);
        }
    }

    public async Task<string?> Commit(Guid pageId, string message)
    {
        try
        {
            var revisionId = await _store.CommitToRevision(pageId, Context.User!, message);
            
            _logger.LogInformation("Committed page {PageId} to revision {RevisionId}", pageId, revisionId);
            
            return revisionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit page {PageId}", pageId);
            await Clients.Caller.SendAsync("Error", "Failed to commit changes");
            return null;
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔗 Client {ConnectionId} connected - User: {User}, Authenticated: {IsAuth}", 
            Context.ConnectionId, Context.User?.Identity?.Name ?? "null", Context.User?.Identity?.IsAuthenticated ?? false);
            
        // Log user claims for debugging authorization
        if (Context.User?.Claims != null)
        {
            foreach (var claim in Context.User.Claims)
            {
                _logger.LogDebug("User claim: {Type} = {Value}", claim.Type, claim.Value);
            }
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 Client {ConnectionId} disconnected - Exception: {Exception}", 
            Context.ConnectionId, exception?.Message ?? "none");
        await base.OnDisconnectedAsync(exception);
    }

    // Simple test method to debug SignalR issues
    public async Task TestPush(string message)
    {
        _logger.LogInformation("🧪 TEST PUSH METHOD called with message: {Message}", message);
        await Clients.Caller.SendAsync("TestResponse", "Test push received: " + message);
    }

    private static string Group(Guid sessionId) => $"session:{sessionId}";
}