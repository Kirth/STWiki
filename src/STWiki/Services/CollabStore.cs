using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STWiki.Data;
using STWiki.Data.Entities;

namespace STWiki.Services;

public class CollabOptions
{
    public bool Enabled { get; set; } = true;
    public CheckpointOptions Checkpoint { get; set; } = new();
    public int MaxUpdateBytes { get; set; } = 32768;
    public PresenceOptions Presence { get; set; } = new();
    public bool UseRedis { get; set; } = false;
    public RedisOptions Redis { get; set; } = new();
}

public class CheckpointOptions
{
    public int MaxUpdates { get; set; } = 500;
    public int MaxSeconds { get; set; } = 20;
}

public class PresenceOptions
{
    public bool Enabled { get; set; } = true;
    public int TtlSeconds { get; set; } = 30;
}

public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
}

public class CollabStore : ICollabStore
{
    private readonly AppDbContext _context;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICollabMaterializer _materializer;
    private readonly ActivityService _activityService;
    private readonly UserService _userService;
    private readonly IOptionsMonitor<CollabOptions> _options;
    private readonly ILogger<CollabStore> _logger;

    public CollabStore(
        AppDbContext context,
        IAuthorizationService authorizationService,
        ICollabMaterializer materializer,
        ActivityService activityService,
        UserService userService,
        IOptionsMonitor<CollabOptions> options,
        ILogger<CollabStore> logger)
    {
        _context = context;
        _authorizationService = authorizationService;
        _materializer = materializer;
        _activityService = activityService;
        _userService = userService;
        _options = options;
        _logger = logger;
    }

    public async Task EnsureCanEdit(ClaimsPrincipal user, Guid pageId)
    {
        var page = await _context.Pages.FindAsync(pageId);
        if (page == null)
            throw new InvalidOperationException($"Page {pageId} not found");

        var result = await _authorizationService.AuthorizeAsync(user, page, "RequireEditor");
        if (!result.Succeeded)
            throw new UnauthorizedAccessException("User cannot edit this page");
    }

    public async Task<CollabSession> GetOrCreateActiveSession(Guid pageId)
    {
        var activeSession = await _context.CollabSessions
            .Where(s => s.PageId == pageId && s.ClosedAt == null)
            .FirstOrDefaultAsync();

        if (activeSession != null)
            return activeSession;

        // Get the current saved page content to initialize the session
        var page = await _context.Pages.FindAsync(pageId);
        byte[]? initialCheckpoint = null;
        
        if (page != null && !string.IsNullOrEmpty(page.Body))
        {
            // Convert page content to collaborative format (simple JSON structure)
            var initialContent = new
            {
                type = "content_update",
                content = page.Body,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                clientId = "system"
            };
            
            initialCheckpoint = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(initialContent));
            
            _logger.LogInformation("Initializing new collaboration session with saved page content ({ContentLength} characters)",
                page.Body.Length);
        }

        var newSession = new CollabSession
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            CreatedAt = DateTimeOffset.UtcNow,
            CheckpointVersion = 0,
            CheckpointBytes = initialCheckpoint
        };

        _context.CollabSessions.Add(newSession);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new collaboration session {SessionId} for page {PageId}", 
            newSession.Id, pageId);

        return newSession;
    }

    public async Task<CollabSession> RequireActiveSession(Guid pageId)
    {
        var session = await _context.CollabSessions
            .Where(s => s.PageId == pageId && s.ClosedAt == null)
            .FirstOrDefaultAsync();

        if (session == null)
            throw new InvalidOperationException($"No active collaboration session for page {pageId}");

        return session;
    }

    public async Task<InitPayload> GetInitPayload(Guid sessionId, string clientVectorJson)
    {
        var session = await _context.CollabSessions
            .Include(s => s.Updates)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        // Get updates since checkpoint
        var updates = await _context.CollabUpdates
            .Where(u => u.SessionId == sessionId && u.Id > session.CheckpointVersion)
            .OrderBy(u => u.Id)
            .Select(u => u.UpdateBytes)
            .ToListAsync();

        return new InitPayload(session.CheckpointVersion, session.CheckpointBytes, updates);
    }

    public async Task<long> AppendUpdate(Guid sessionId, Guid clientId, byte[] updateBytes, string clientVectorJson)
    {
        if (updateBytes.Length > _options.CurrentValue.MaxUpdateBytes)
            throw new InvalidOperationException($"Update size {updateBytes.Length} exceeds limit");

        var update = new CollabUpdate
        {
            SessionId = sessionId,
            ClientId = clientId,
            UpdateBytes = updateBytes,
            VectorClockJson = clientVectorJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.CollabUpdates.Add(update);
        await _context.SaveChangesAsync();

        _logger.LogDebug("Appended update {UpdateId} from client {ClientId} to session {SessionId}", 
            update.Id, clientId, sessionId);

        return update.Id;
    }

    public async Task<string> CommitToRevision(Guid pageId, ClaimsPrincipal user, string message)
    {
        var session = await RequireActiveSession(pageId);
        
        // Force checkpoint to get latest state
        await PerformCheckpointIfDue(session.Id);
        await _context.Entry(session).ReloadAsync();

        if (session.CheckpointBytes == null)
            throw new InvalidOperationException("No content to commit");

        // Materialize content from CRDT snapshot
        var (title, summary, body, bodyFormat) = _materializer.Materialize(session.CheckpointBytes);

        // Get user info
        var userInfo = await _userService.GetOrCreateUserAsync(user);

        // Create revision
        var revision = new Revision
        {
            PageId = pageId,
            Author = userInfo.DisplayName,
            Note = message,
            Snapshot = body,
            Format = bodyFormat,
            YjsUpdate = session.CheckpointBytes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Revisions.Add(revision);

        // Update page
        var page = await _context.Pages.FindAsync(pageId);
        if (page != null)
        {
            page.Title = title;
            page.Summary = summary;
            page.Body = body;
            page.BodyFormat = bodyFormat;
            page.UpdatedAt = DateTimeOffset.UtcNow;
            page.UpdatedBy = userInfo.DisplayName;
            page.LastCommittedAt = DateTimeOffset.UtcNow;
            page.HasUncommittedChanges = false;
            page.LastCommittedContent = body;
        }

        await _context.SaveChangesAsync();

        // Log activity
        await _activityService.LogAsync(
            "Commit",
            userInfo.UserId,
            userInfo.DisplayName,
            pageId,
            page?.Slug ?? "",
            title,
            $"Committed collaborative changes: {message}");

        _logger.LogInformation("Committed session {SessionId} to revision {RevisionId}", 
            session.Id, revision.Id);

        return revision.Id.ToString();
    }

    public async Task PerformCheckpointIfDue(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.CollabSessions
            .Include(s => s.Updates)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null) return;

        var options = _options.CurrentValue.Checkpoint;
        var updatesSinceCheckpoint = session.Updates.Count(u => u.Id > session.CheckpointVersion);
        var timeSinceLastCheckpoint = DateTimeOffset.UtcNow - session.CreatedAt;

        bool shouldCheckpoint = updatesSinceCheckpoint >= options.MaxUpdates || 
                               timeSinceLastCheckpoint.TotalSeconds >= options.MaxSeconds;

        if (!shouldCheckpoint) return;

        // Get all updates since last checkpoint
        var updates = await _context.CollabUpdates
            .Where(u => u.SessionId == sessionId && u.Id > session.CheckpointVersion)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        if (!updates.Any()) return;

        // Apply updates to create new checkpoint
        // Since our updates are full content replacements (content_update), 
        // the latest update contains the complete current state
        var latestUpdate = updates.Last();
        
        // Verify this is a content update with full state
        try
        {
            var updateJson = System.Text.Encoding.UTF8.GetString(latestUpdate.UpdateBytes);
            var updateObj = JsonSerializer.Deserialize<JsonElement>(updateJson);
            
            if (updateObj.TryGetProperty("type", out var typeElement) && 
                typeElement.GetString() == "content_update" &&
                updateObj.TryGetProperty("content", out var contentElement))
            {
                // This is a valid content update - use it as checkpoint
                session.CheckpointBytes = latestUpdate.UpdateBytes;
                _logger.LogInformation("Created checkpoint for session {SessionId} from {UpdateCount} updates", 
                    sessionId, updates.Count);
            }
            else
            {
                _logger.LogWarning("Latest update for session {SessionId} is not a content_update, keeping existing checkpoint", 
                    sessionId);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse latest update for session {SessionId}, keeping existing checkpoint", 
                sessionId);
            return;
        }
        session.CheckpointVersion = latestUpdate.Id;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created checkpoint for session {SessionId} at version {Version}", 
            sessionId, session.CheckpointVersion);
    }

    public async Task<IEnumerable<CollabSession>> GetSessionsNeedingCheckpoint(CancellationToken ct = default)
    {
        var options = _options.CurrentValue.Checkpoint;
        var cutoffTime = DateTimeOffset.UtcNow.AddSeconds(-options.MaxSeconds);

        return await _context.CollabSessions
            .Include(s => s.Updates)
            .Where(s => s.ClosedAt == null)
            .Where(s => s.Updates.Count(u => u.Id > s.CheckpointVersion) >= options.MaxUpdates ||
                       s.CreatedAt <= cutoffTime)
            .ToListAsync(ct);
    }

    public async Task BroadcastPresence(Guid pageId, string presenceJson, string groupName,
        Func<string, IClientProxy> othersInGroupAccessor)
    {
        if (!_options.CurrentValue.Presence.Enabled) return;

        var session = await _context.CollabSessions
            .Where(s => s.PageId == pageId && s.ClosedAt == null)
            .FirstOrDefaultAsync();

        if (session == null) return;

        // Update session awareness
        session.AwarenessJson = presenceJson;
        await _context.SaveChangesAsync();

        // Broadcast to other clients
        await othersInGroupAccessor(groupName).SendAsync("Presence", presenceJson);
    }
}