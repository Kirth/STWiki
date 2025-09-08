using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using STWiki.Data.Entities;

namespace STWiki.Services;

public record InitPayload(long Version, byte[]? Checkpoint, IReadOnlyList<byte[]> Updates);

public interface ICollabStore
{
    Task EnsureCanEdit(ClaimsPrincipal user, Guid pageId);
    Task<CollabSession> GetOrCreateActiveSession(Guid pageId);
    Task<CollabSession> RequireActiveSession(Guid pageId);
    Task<InitPayload> GetInitPayload(Guid sessionId, string clientVectorJson);
    Task<long> AppendUpdate(Guid sessionId, Guid clientId, byte[] updateBytes, string clientVectorJson);
    Task<string> CommitToRevision(Guid pageId, ClaimsPrincipal user, string message);
    Task PerformCheckpointIfDue(Guid sessionId, CancellationToken ct = default);
    Task<IEnumerable<CollabSession>> GetSessionsNeedingCheckpoint(CancellationToken ct = default);
    Task BroadcastPresence(Guid pageId, string presenceJson, string groupName,
        Func<string, IClientProxy> othersInGroupAccessor);
}