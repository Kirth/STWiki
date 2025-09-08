using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class CollabUpdate
{
    public long Id { get; set; }
    
    public Guid SessionId { get; set; }
    
    public Guid ClientId { get; set; }
    
    [MaxLength(2000)]
    public string VectorClockJson { get; set; } = "{}";
    
    public byte[] UpdateBytes { get; set; } = Array.Empty<byte>();
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual CollabSession Session { get; set; } = null!;
}