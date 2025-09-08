using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class CollabSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid PageId { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? ClosedAt { get; set; }
    
    public long CheckpointVersion { get; set; }
    
    public byte[]? CheckpointBytes { get; set; }
    
    [MaxLength(4000)]
    public string? AwarenessJson { get; set; }
    
    public virtual Page Page { get; set; } = null!;
    
    public virtual ICollection<CollabUpdate> Updates { get; set; } = new List<CollabUpdate>();
    
    public virtual ICollection<CollabCheckpoint> Checkpoints { get; set; } = new List<CollabCheckpoint>();
}