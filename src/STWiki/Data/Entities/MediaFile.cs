using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class MediaFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = "";
    
    [Required]
    [MaxLength(100)]
    public string StoredFileName { get; set; } = "";
    
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = "";
    
    public long FileSize { get; set; }
    
    [MaxLength(500)]
    public string ObjectKey { get; set; } = "";
    
    [MaxLength(50)]
    public string BucketName { get; set; } = "";
    
    [MaxLength(1000)]
    public string Description { get; set; } = "";
    
    [MaxLength(500)]
    public string AltText { get; set; } = "";
    
    // User relationship
    public long? UploadedByUserId { get; set; }
    public virtual User? UploadedBy { get; set; }
    
    // Timestamps
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    
    // Access control
    public bool IsPublic { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    
    // Image-specific properties
    public int? Width { get; set; }
    public int? Height { get; set; }
    
    // File relationships
    public virtual ICollection<MediaThumbnail> Thumbnails { get; set; } = new List<MediaThumbnail>();
    public virtual ICollection<PageMediaReference> PageReferences { get; set; } = new List<PageMediaReference>();
}