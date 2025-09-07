namespace STWiki.Services;

public interface IObjectStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadFileAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string objectKey, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default);
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default);
}

public class ObjectStorageFile
{
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTimeOffset LastModified { get; set; }
}