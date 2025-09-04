using STWiki.Services.Interfaces;

namespace STWiki.Services.Implementation;

/// <summary>
/// Background service for cleaning up inactive collaboration sessions
/// </summary>
public class CollaborationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CollaborationCleanupService> _logger;
    
    // Configuration - these could be made configurable
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _inactivityThreshold = TimeSpan.FromHours(1);
    
    public CollaborationCleanupService(
        IServiceProvider serviceProvider,
        ILogger<CollaborationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Collaboration cleanup service started");
        
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupInactiveSessionsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Collaboration cleanup service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in collaboration cleanup service");
            throw;
        }
    }
    
    private async Task CleanupInactiveSessionsAsync()
    {
        try
        {
            _logger.LogDebug("Starting cleanup of inactive collaboration sessions");
            
            using var scope = _serviceProvider.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
            
            await sessionService.CleanupInactiveSessionsAsync(_inactivityThreshold);
            _logger.LogDebug("Completed cleanup of inactive collaboration sessions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during collaboration session cleanup");
            // Don't rethrow - we want the service to continue running
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collaboration cleanup service stopping");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Collaboration cleanup service stopped");
    }
}