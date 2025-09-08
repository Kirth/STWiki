using Microsoft.Extensions.Options;

namespace STWiki.Services;

public class CollabCheckpointService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CollabOptions> _options;
    private readonly ILogger<CollabCheckpointService> _logger;

    public CollabCheckpointService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CollabOptions> options,
        ILogger<CollabCheckpointService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CollabCheckpointService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_options.CurrentValue.Enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var collabStore = scope.ServiceProvider.GetRequiredService<ICollabStore>();

                var sessionsNeedingCheckpoint = await collabStore.GetSessionsNeedingCheckpoint(stoppingToken);
                var count = 0;

                foreach (var session in sessionsNeedingCheckpoint)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await collabStore.PerformCheckpointIfDue(session.Id, stoppingToken);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to checkpoint session {SessionId}", session.Id);
                    }
                }

                if (count > 0)
                {
                    _logger.LogDebug("Processed {Count} sessions for checkpointing", count);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CollabCheckpointService");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("CollabCheckpointService stopped");
    }
}