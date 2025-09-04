using STWiki.Services.Interfaces;
using STWiki.Services.Implementation;
using STWiki.Repositories.Interfaces;
using STWiki.Repositories.Implementation;

namespace STWiki.Extensions;

/// <summary>
/// Extension methods for registering services in the DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the new refactored collaboration services
    /// </summary>
    public static IServiceCollection AddCollaborativeEditing(this IServiceCollection services)
    {
        // Core services
        services.AddScoped<IEditorService, EditorService>();
        services.AddScoped<ICollaborationService, CollaborationService>();
        services.AddScoped<ICollaborationSessionService, CollaborationSessionService>();
        services.AddScoped<IOperationTransformService, OperationTransformService>();
        
        // Infrastructure services
        services.AddScoped<ISignalRConnectionService, SignalRConnectionService>();
        services.AddScoped<IJavaScriptEditorService, JavaScriptEditorService>();
        
        // Repositories (for now, in-memory implementations)
        services.AddSingleton<ICollaborationSessionRepository, InMemoryCollaborationSessionRepository>();
        
        // Background services
        services.AddHostedService<CollaborationCleanupService>();
        
        return services;
    }
    
    /// <summary>
    /// Register SignalR with collaboration hub
    /// </summary>
    public static IServiceCollection AddCollaborationSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
            options.StreamBufferCapacity = 10;
        });
        
        return services;
    }
}