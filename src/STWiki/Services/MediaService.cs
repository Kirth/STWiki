using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using STWiki.Data;
using STWiki.Data.Entities;
using STWiki.Models;

namespace STWiki.Services;

public class MediaService : IMediaService
{
    private readonly AppDbContext _context;
    private readonly IObjectStorageService _storageService;
    private readonly ActivityService _activityService;
    private readonly ILogger<MediaService> _logger;
    private readonly MediaConfiguration _mediaConfig;

    public MediaService(
        AppDbContext context,
        IObjectStorageService storageService,
        ActivityService activityService,
        ILogger<MediaService> logger,
        IOptions<MediaConfiguration> mediaConfig)
    {
        _context = context;
        _storageService = storageService;
        _activityService = activityService;
        _logger = logger;
        _mediaConfig = mediaConfig.Value;
    }

    public async Task<MediaUploadResult> UploadFileAsync(
        IFormFile file, 
        string? filename,
        string? description, 
        string? altText, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = ValidateFile(file);
            if (!validationResult.IsValid)
            {
                return new MediaUploadResult 
                { 
                    Success = false, 
                    ValidationErrors = validationResult.Errors 
                };
            }

            var userUsage = await GetMediaUsageAsync(userId, cancellationToken);
            if (userUsage.TotalFiles >= _mediaConfig.MaxFilesPerUser)
            {
                return new MediaUploadResult
                {
                    Success = false,
                    ErrorMessage = "File upload quota exceeded"
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            
            string objectKey;
            long finalFileSize;
            (int? width, int? height) dimensions = (null, null);

            using (var stream = file.OpenReadStream())
            {
                if (IsImage(file.ContentType) && _mediaConfig.EnableImageOptimization)
                {
                    using var processedStream = await ProcessImageAsync(stream, file.ContentType);
                    objectKey = await _storageService.UploadFileAsync(processedStream, file.FileName, file.ContentType, cancellationToken);
                    finalFileSize = processedStream.Length;
                    dimensions = await GetImageDimensionsAsync(processedStream);
                }
                else
                {
                    objectKey = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType, cancellationToken);
                    finalFileSize = file.Length;
                    if (IsImage(file.ContentType))
                    {
                        dimensions = await GetImageDimensionsAsync(stream);
                    }
                }
            }

            // Use custom filename if provided, otherwise use original filename
            var finalFileName = !string.IsNullOrWhiteSpace(filename) ? filename.Trim() : Path.GetFileNameWithoutExtension(file.FileName);
            
            // Ensure filename has the correct extension
            var extension = Path.GetExtension(file.FileName);
            if (!string.IsNullOrEmpty(extension) && !finalFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                finalFileName += extension;
            }

            var mediaFile = new MediaFile
            {
                OriginalFileName = finalFileName,
                StoredFileName = Path.GetFileName(objectKey),
                ContentType = file.ContentType,
                FileSize = finalFileSize,
                ObjectKey = objectKey,
                BucketName = "stwiki-media", // TODO: Get from config
                Description = description ?? "",
                AltText = altText ?? "",
                UploadedByUserId = user?.Id,
                Width = dimensions.width,
                Height = dimensions.height,
                IsPublic = _mediaConfig.AllowPublicAccess
            };

            _context.MediaFiles.Add(mediaFile);
            await _context.SaveChangesAsync(cancellationToken);

            if (IsImage(file.ContentType))
            {
                _ = Task.Run(() => GenerateThumbnailsAsync(mediaFile.Id, CancellationToken.None));
            }

            await _activityService.LogAsync(
                ActivityTypes.MediaUploaded,
                userId,
                user?.DisplayName ?? "Unknown User",
                description: $"Uploaded media file: {file.FileName}",
                details: new { 
                    MediaFileId = mediaFile.Id, 
                    FileName = file.FileName, 
                    FileSize = finalFileSize,
                    ContentType = file.ContentType
                });

            _logger.LogInformation("Media file uploaded successfully: {FileName} by user {UserId}", 
                file.FileName, userId);

            return new MediaUploadResult
            {
                Success = true,
                MediaFile = mediaFile
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload media file: {FileName}", file.FileName);
            return new MediaUploadResult
            {
                Success = false,
                ErrorMessage = "File upload failed"
            };
        }
    }

    public async Task<MediaFile?> GetMediaFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MediaFiles
            .Include(m => m.UploadedBy)
            .Include(m => m.Thumbnails)
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken);
    }

    public async Task<MediaFile?> GetMediaFileByNameAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return await _context.MediaFiles
            .Include(m => m.UploadedBy)
            .Include(m => m.Thumbnails)
            .FirstOrDefaultAsync(m => m.OriginalFileName == fileName && !m.IsDeleted, cancellationToken);
    }

    public async Task<List<MediaFile>> GetUserMediaAsync(string userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null) return new List<MediaFile>();

        return await _context.MediaFiles
            .Include(m => m.Thumbnails)
            .Where(m => m.UploadedByUserId == user.Id && !m.IsDeleted)
            .OrderByDescending(m => m.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MediaFile>> SearchMediaAsync(string query, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return await _context.MediaFiles
            .Include(m => m.Thumbnails)
            .Where(m => !m.IsDeleted && 
                (m.OriginalFileName.Contains(query) || 
                 m.Description.Contains(query) || 
                 m.AltText.Contains(query)))
            .OrderByDescending(m => m.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteMediaAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var mediaFile = await GetMediaFileAsync(id, cancellationToken);
            if (mediaFile == null) return false;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            
            // Check if user owns the file or is admin
            if (mediaFile.UploadedByUserId != user?.Id)
            {
                // TODO: Add admin check
                return false;
            }

            // Mark as deleted instead of hard delete
            mediaFile.IsDeleted = true;
            mediaFile.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _context.SaveChangesAsync(cancellationToken);

            // Delete from storage in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _storageService.DeleteFileAsync(mediaFile.ObjectKey, CancellationToken.None);
                    
                    // Delete thumbnails
                    foreach (var thumbnail in mediaFile.Thumbnails)
                    {
                        await _storageService.DeleteFileAsync(thumbnail.ObjectKey, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete media files from storage for MediaFile {MediaFileId}", id);
                }
            });

            await _activityService.LogAsync(
                ActivityTypes.MediaDeleted,
                userId,
                user?.DisplayName ?? "Unknown User",
                description: $"Deleted media file: {mediaFile.OriginalFileName}",
                details: new { MediaFileId = id, FileName = mediaFile.OriginalFileName });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file {MediaFileId}", id);
            return false;
        }
    }

    public async Task<bool> UpdateMediaAsync(Guid id, string? description, string? altText, string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var mediaFile = await GetMediaFileAsync(id, cancellationToken);
            if (mediaFile == null) return false;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            
            if (mediaFile.UploadedByUserId != user?.Id)
            {
                return false;
            }

            mediaFile.Description = description ?? mediaFile.Description;
            mediaFile.AltText = altText ?? mediaFile.AltText;
            mediaFile.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            await _activityService.LogAsync(
                ActivityTypes.MediaUpdated,
                userId,
                user?.DisplayName ?? "Unknown User",
                description: $"Updated media file: {mediaFile.OriginalFileName}",
                details: new { MediaFileId = id, Description = description, AltText = altText });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update media file {MediaFileId}", id);
            return false;
        }
    }

    public async Task<string> GetMediaUrlAsync(Guid id, int? thumbnailSize = null, CancellationToken cancellationToken = default)
    {
        var mediaFile = await GetMediaFileAsync(id, cancellationToken);
        if (mediaFile == null) return "";

        if (thumbnailSize.HasValue && IsImage(mediaFile.ContentType))
        {
            var thumbnail = mediaFile.Thumbnails.FirstOrDefault(t => t.Width == thumbnailSize.Value);
            if (thumbnail != null)
            {
                return await _storageService.GetPresignedUrlAsync(thumbnail.ObjectKey, TimeSpan.FromHours(1), cancellationToken);
            }
        }

        return await _storageService.GetPresignedUrlAsync(mediaFile.ObjectKey, TimeSpan.FromHours(1), cancellationToken);
    }

    public async Task<Stream?> GetMediaStreamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var mediaFile = await GetMediaFileAsync(id, cancellationToken);
            if (mediaFile == null) return null;

            return await _storageService.DownloadFileAsync(mediaFile.ObjectKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media stream for {MediaFileId}", id);
            return null;
        }
    }

    public async Task<bool> GenerateThumbnailsAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        try
        {
            var mediaFile = await GetMediaFileAsync(mediaId, cancellationToken);
            if (mediaFile == null || !IsImage(mediaFile.ContentType)) return false;

            using var originalStream = await _storageService.DownloadFileAsync(mediaFile.ObjectKey, cancellationToken);
            using var image = await Image.LoadAsync(originalStream, cancellationToken);

            foreach (var size in _mediaConfig.ThumbnailSizes)
            {
                if (image.Width <= size) continue; // Skip if original is smaller

                var thumbnail = image.Clone(ctx => ctx.Resize(size, 0));
                
                using var thumbnailStream = new MemoryStream();
                await thumbnail.SaveAsJpegAsync(thumbnailStream, cancellationToken);
                thumbnailStream.Position = 0;

                var thumbnailFileName = $"{Path.GetFileNameWithoutExtension(mediaFile.OriginalFileName)}_thumb_{size}.jpg";
                var thumbnailObjectKey = await _storageService.UploadFileAsync(
                    thumbnailStream, 
                    thumbnailFileName, 
                    "image/jpeg", 
                    cancellationToken);

                var mediaThumb = new MediaThumbnail
                {
                    MediaFileId = mediaId,
                    StoredFileName = thumbnailFileName,
                    ObjectKey = thumbnailObjectKey,
                    BucketName = "stwiki-media",
                    Width = size,
                    Height = (thumbnail.Height * size) / thumbnail.Width,
                    FileSize = thumbnailStream.Length
                };

                _context.MediaThumbnails.Add(mediaThumb);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnails for media {MediaId}", mediaId);
            return false;
        }
    }

    public async Task<MediaUsageReport> GetMediaUsageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null) return new MediaUsageReport();

        var mediaFiles = await _context.MediaFiles
            .Where(m => m.UploadedByUserId == user.Id && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var report = new MediaUsageReport
        {
            TotalFiles = mediaFiles.Count,
            TotalSize = mediaFiles.Sum(m => m.FileSize),
            PublicFiles = mediaFiles.Count(m => m.IsPublic),
            PrivateFiles = mediaFiles.Count(m => !m.IsPublic)
        };

        report.FileTypeBreakdown = mediaFiles
            .GroupBy(m => m.ContentType)
            .ToDictionary(g => g.Key, g => g.Count());

        return report;
    }

    private async Task<Stream> ProcessImageAsync(Stream inputStream, string contentType)
    {
        var outputStream = new MemoryStream();
        
        using var image = await Image.LoadAsync(inputStream);
        
        if (image.Width > _mediaConfig.MaxImageDimension || image.Height > _mediaConfig.MaxImageDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_mediaConfig.MaxImageDimension, _mediaConfig.MaxImageDimension),
                Mode = ResizeMode.Max
            }));
        }

        await image.SaveAsJpegAsync(outputStream);
        outputStream.Position = 0;
        
        return outputStream;
    }

    private async Task<(int? width, int? height)> GetImageDimensionsAsync(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var image = await Image.LoadAsync(stream);
            return (image.Width, image.Height);
        }
        catch
        {
            return (null, null);
        }
    }

    private (bool IsValid, List<string> Errors) ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        if (file.Length == 0)
            errors.Add("File is empty");

        if (file.Length > _mediaConfig.MaxFileSize)
            errors.Add($"File size exceeds maximum allowed size of {_mediaConfig.MaxFileSize / 1024 / 1024}MB");

        if (!_mediaConfig.AllowedTypes.Contains(file.ContentType))
            errors.Add($"File type '{file.ContentType}' is not allowed");

        return (errors.Count == 0, errors);
    }

    private static bool IsImage(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}