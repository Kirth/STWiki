using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using STWiki.Models;

namespace STWiki.Services;

public class MinIOStorageService : IObjectStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ObjectStorageConfiguration _config;
    private readonly ILogger<MinIOStorageService> _logger;

    public MinIOStorageService(IMinioClient minioClient, IOptions<ObjectStorageConfiguration> config, ILogger<MinIOStorageService> logger)
    {
        _minioClient = minioClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var objectKey = GenerateObjectKey(fileName);
            
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectKey)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            _logger.LogInformation("Successfully uploaded file {FileName} with object key {ObjectKey}", fileName, objectKey);
            
            return objectKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file with object key {ObjectKey}", objectKey);
            throw;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectKey)
                .WithExpiry((int)expiration.TotalSeconds);

            var url = await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
            
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for object key {ObjectKey}", objectKey);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectKey);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);

            _logger.LogInformation("Successfully deleted file with object key {ObjectKey}", objectKey);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file with object key {ObjectKey}", objectKey);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_config.BucketName)
                .WithObject(objectKey);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task EnsureBucketExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(_config.BucketName);

            var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);

            if (!exists)
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(_config.BucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
                
                _logger.LogInformation("Created bucket {BucketName}", _config.BucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket {BucketName} exists", _config.BucketName);
            throw;
        }
    }

    private string GenerateObjectKey(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueId = Guid.NewGuid().ToString();
        var date = DateTimeOffset.UtcNow.ToString("yyyy/MM/dd");
        
        return $"{date}/{uniqueId}{extension}";
    }
}