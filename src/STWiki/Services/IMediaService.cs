using STWiki.Data.Entities;

namespace STWiki.Services;

public interface IMediaService
{
    Task<MediaUploadResult> UploadFileAsync(IFormFile file, string? filename, string? description, string? altText, string userId, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetMediaFileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MediaFile?> GetMediaFileByNameAsync(string fileName, CancellationToken cancellationToken = default);
    Task<List<MediaFile>> GetUserMediaAsync(string userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<List<MediaFile>> SearchMediaAsync(string query, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<bool> DeleteMediaAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateMediaAsync(Guid id, string? description, string? altText, string userId, CancellationToken cancellationToken = default);
    Task<string> GetMediaUrlAsync(Guid id, int? thumbnailSize = null, CancellationToken cancellationToken = default);
    Task<bool> GenerateThumbnailsAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<MediaUsageReport> GetMediaUsageAsync(string userId, CancellationToken cancellationToken = default);
    Task<Stream?> GetMediaStreamAsync(Guid id, CancellationToken cancellationToken = default);
}

public class MediaUploadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public MediaFile? MediaFile { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

public class MediaUsageReport
{
    public int TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public int PublicFiles { get; set; }
    public int PrivateFiles { get; set; }
    public Dictionary<string, int> FileTypeBreakdown { get; set; } = new();
}