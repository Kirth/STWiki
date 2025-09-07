namespace STWiki.Data.Entities;

public class PageMediaReference
{
    public long Id { get; set; }
    public Guid PageId { get; set; }
    public virtual Page Page { get; set; } = null!;
    
    public Guid MediaFileId { get; set; }
    public virtual MediaFile MediaFile { get; set; } = null!;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}