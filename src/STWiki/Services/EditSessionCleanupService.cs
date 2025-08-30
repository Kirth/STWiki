using Microsoft.Extensions.Options;
using STWiki.Models;
using STWiki.Services;

namespace STWiki.BackgroundServices;

public class EditSessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EditSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;

    public EditSessionCleanupService(IServiceProvider serviceProvider, ILogger<EditSessionCleanupService> logger, IOptions<CollaborationOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cleanupInterval = TimeSpan.FromMinutes(options.Value.AutoCleanupIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Edit session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var editSessionService = scope.ServiceProvider.GetRequiredService<IEditSessionService>();
                
                await editSessionService.CleanupIdleSessionsAsync();
                
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during edit session cleanup");
                
                // Wait a bit before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        
        _logger.LogInformation("Edit session cleanup service stopped");
    }
}