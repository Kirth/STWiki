using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using STWiki.Services.Diff;

namespace STWiki.Services;

public interface IDiffCacheService
{
    Task<T?> GetCachedDiffAsync<T>(string key) where T : class;
    Task SetCachedDiffAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task InvalidateDiffCacheAsync(long pageId);
    string GenerateDiffKey(long fromRevisionId, long toRevisionId, DiffOptions options);
}

public class DiffCacheService : IDiffCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly DiffCacheOptions _options;
    private readonly ILogger<DiffCacheService> _logger;

    public DiffCacheService(IMemoryCache memoryCache, IOptions<DiffCacheOptions> options, ILogger<DiffCacheService> logger)
    {
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> GetCachedDiffAsync<T>(string key) where T : class
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue))
            {
                _logger.LogDebug("Cache hit for diff key: {Key}", key);
                return cachedValue as T;
            }

            _logger.LogDebug("Cache miss for diff key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached diff for key: {Key}", key);
            return null;
        }
    }

    public async Task SetCachedDiffAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var cacheExpiration = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
            
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                Size = EstimateObjectSize(value),
                Priority = CacheItemPriority.Normal,
                SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes)
            };

            _memoryCache.Set(key, value, cacheEntryOptions);
            _logger.LogDebug("Cached diff result for key: {Key} (expires in {Expiration})", key, cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching diff result for key: {Key}", key);
        }
    }

    public async Task InvalidateDiffCacheAsync(long pageId)
    {
        try
        {
            var keysToRemove = new List<object>();
            
            if (_memoryCache is MemoryCache mc)
            {
                var field = typeof(MemoryCache).GetField("_coherentState", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field?.GetValue(mc) is object coherentState)
                {
                    var entriesProperty = coherentState.GetType().GetProperty("EntriesCollection",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (entriesProperty?.GetValue(coherentState) is System.Collections.IDictionary entries)
                    {
                        foreach (System.Collections.DictionaryEntry entry in entries)
                        {
                            if (entry.Key is string key && key.Contains($"page_{pageId}_"))
                            {
                                keysToRemove.Add(entry.Key);
                            }
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
            }

            _logger.LogDebug("Invalidated {Count} cached diff entries for page {PageId}", keysToRemove.Count, pageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating diff cache for page {PageId}", pageId);
        }
    }

    public string GenerateDiffKey(long fromRevisionId, long toRevisionId, DiffOptions options)
    {
        var optionsJson = JsonSerializer.Serialize(new
        {
            options.Granularity,
            options.IgnoreWhitespace,
            options.ShowStats,
            options.ContextLines,
            options.ViewMode
        });

        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(optionsJson));
        var optionsHash = Convert.ToHexString(hash)[..8];

        return $"diff_{fromRevisionId}_{toRevisionId}_{optionsHash}";
    }

    private long EstimateObjectSize(object obj)
    {
        try
        {
            var json = JsonSerializer.Serialize(obj);
            return System.Text.Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            return 1024;
        }
    }
}

public class DiffCacheOptions
{
    public const string SectionName = "DiffCache";
    
    public int DefaultExpirationMinutes { get; set; } = 60;
    public int SlidingExpirationMinutes { get; set; } = 30;
    public long MaxCacheSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public bool EnableCaching { get; set; } = true;
}

public class DiffOptions
{
    public DiffGranularity Granularity { get; set; } = DiffGranularity.Line;
    public bool IgnoreWhitespace { get; set; } = false;
    public bool ShowStats { get; set; } = true;
    public int ContextLines { get; set; } = 3;
    public DiffViewMode ViewMode { get; set; } = DiffViewMode.Unified;
}

public enum DiffGranularity
{
    Line,
    Word,
    Character
}

public enum DiffViewMode
{
    Unified,
    SideBySide,
    Inline,
    Stats
}