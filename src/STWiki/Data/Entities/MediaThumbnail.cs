using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class MediaThumbnail
{
    public long Id { get; set; }
    public Guid MediaFileId { get; set; }
    public virtual MediaFile MediaFile { get; set; } = null!;
    
    [Required]
    [MaxLength(100)]
    public string StoredFileName { get; set; } = "";
    
    [MaxLength(500)]
    public string ObjectKey { get; set; } = "";
    
    [MaxLength(50)]
    public string BucketName { get; set; } = "";
    
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSize { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}