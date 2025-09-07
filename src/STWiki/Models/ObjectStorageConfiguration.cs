namespace STWiki.Models;

public class ObjectStorageConfiguration
{
    public const string SectionName = "ObjectStorage";
    
    public string Provider { get; set; } = "MinIO";
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "stwiki-media";
    public string Region { get; set; } = "us-east-1";
    public bool UseSSL { get; set; } = false;
}

public class MediaConfiguration
{
    public const string SectionName = "Media";
    
    public long MaxFileSize { get; set; } = 52428800; // 50MB
    public int MaxFilesPerUser { get; set; } = 1000;
    public List<string> AllowedTypes { get; set; } = new();
    public List<int> ThumbnailSizes { get; set; } = new() { 150, 300, 600, 1200 };
    public bool EnableImageOptimization { get; set; } = true;
    public int ImageQuality { get; set; } = 85;
    public int MaxImageDimension { get; set; } = 2048;
    public bool AllowPublicAccess { get; set; } = true;
    public bool RequireDescriptions { get; set; } = false;
}