namespace STWiki.Data.Entities;

public class CollabCheckpoint
{
    public long Id { get; set; }
    
    public Guid SessionId { get; set; }
    
    public long Version { get; set; }
    
    public byte[] SnapshotBytes { get; set; } = Array.Empty<byte>();
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual CollabSession Session { get; set; } = null!;
}