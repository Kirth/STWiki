namespace STWiki.Models;

public class CollaborationOptions
{
    public const string SectionName = "Collaboration";
    
    public bool EnableRealTimeEdit { get; set; } = true;
    public int MaxConcurrentUsers { get; set; } = 10;
    public int OperationBatchSize { get; set; } = 50;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int CursorBroadcastIntervalMs { get; set; } = 1000;
    public int AutoCleanupIntervalMinutes { get; set; } = 10;
    public int MaxOperationHistorySize { get; set; } = 1000;
}