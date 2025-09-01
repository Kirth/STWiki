using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Minio;
using STWiki.Data;
using STWiki.Models;
using STWiki.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRazorPages(options =>
{
    // Set route ordering to ensure Edit pages are processed before View pages  
    options.Conventions.AddPageRoute("/Wiki/Edit", "/{*slug:regex(.*\\/edit$)}");
    options.Conventions.AddPageRoute("/Wiki/Edit", "/edit");
    options.Conventions.AddPageRoute("/Wiki/History", "/{*slug:regex(.*\\/history$)}");
    options.Conventions.AddPageRoute("/Wiki/History", "/history");
});
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Add custom services
builder.Services.AddSingleton<STWiki.Services.MarkdownService>();
builder.Services.AddSingleton<STWiki.Services.DiffService>();
builder.Services.AddScoped<STWiki.Services.TemplateService>();
builder.Services.AddScoped<STWiki.Services.IRedirectService, STWiki.Services.RedirectService>();
builder.Services.AddSingleton<STWiki.Services.IEditSessionService, STWiki.Services.EditSessionService>();
builder.Services.AddScoped<STWiki.Services.ActivityService>();
builder.Services.AddScoped<STWiki.Services.BreadcrumbService>();
builder.Services.AddScoped<STWiki.Services.IPageHierarchyService, STWiki.Services.PageHierarchyService>();
builder.Services.AddScoped<STWiki.Services.AdvancedSearchService>();
builder.Services.AddScoped<STWiki.Services.UserService>();
builder.Services.AddScoped<STWiki.Services.AdminService>();
builder.Services.AddHttpClient();
builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformation>();

// Add configuration options
builder.Services.Configure<STWiki.Models.CollaborationOptions>(
    builder.Configuration.GetSection(STWiki.Models.CollaborationOptions.SectionName));
builder.Services.Configure<ObjectStorageConfiguration>(
    builder.Configuration.GetSection(ObjectStorageConfiguration.SectionName));
builder.Services.Configure<MediaConfiguration>(
    builder.Configuration.GetSection(MediaConfiguration.SectionName));

// Add media services
var storageConfig = builder.Configuration.GetSection(ObjectStorageConfiguration.SectionName).Get<ObjectStorageConfiguration>() 
    ?? new ObjectStorageConfiguration();

builder.Services.AddSingleton<IMinioClient>(provider =>
{
    var config = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageConfiguration>>().Value;
    return new MinioClient()
        .WithEndpoint(config.Endpoint)
        .WithCredentials(config.AccessKey, config.SecretKey)
        .WithSSL(config.UseSSL)
        .Build();
});

builder.Services.AddScoped<IObjectStorageService, MinIOStorageService>();
builder.Services.AddScoped<IMediaService, MediaService>();

// Add SignalR
builder.Services.AddSignalR();

// Add background services
builder.Services.AddHostedService<STWiki.BackgroundServices.EditSessionCleanupService>();

// Add authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies", options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LoginPath = "/Account/Login";
    
    options.Events.OnRedirectToLogin = context =>
    {
        // Preserve the original URL the user was trying to access
        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"{context.RedirectUri}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return Task.CompletedTask;
    };
})
.AddOpenIdConnect("oidc", options =>
{
    var authSection = builder.Configuration.GetSection("Auth");
    options.Authority = authSection["Authority"];
    options.ClientId = authSection["ClientId"];
    options.ClientSecret = authSection["ClientSecret"];
    options.CallbackPath = authSection["CallbackPath"];
    
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.RequireHttpsMetadata = false; // Allow HTTP for development
    
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("groups");
    
    // Clear default claim mappings that might interfere
    options.ClaimActions.Clear();
    
    // Map standard OIDC claims
    options.ClaimActions.MapUniqueJsonKey("sub", "sub");
    options.ClaimActions.MapUniqueJsonKey("email", "email");
    options.ClaimActions.MapUniqueJsonKey("name", "name");
    options.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");
    options.ClaimActions.MapJsonKey("groups", "groups");
    
    // Add event handlers for debugging
    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("=== TOKEN VALIDATED ===");
            
            if (context.Principal?.Claims != null)
            {
                foreach (var claim in context.Principal.Claims)
                {
                    logger.LogInformation("Token claim: {Type} = {Value}", claim.Type, claim.Value);
                }
            }
            
            // Log user login activity
            Task.Run(async () =>
            {
                try
                {
                    var activityService = context.HttpContext.RequestServices.GetRequiredService<STWiki.Services.ActivityService>();
                    var userName = context.Principal?.Identity?.Name ?? "Unknown";
                    var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                    var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
                    
                    await activityService.LogUserLoginAsync(userName, userName, ipAddress, userAgent);
                    logger.LogInformation("Logged login activity for user: {UserName}", userName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to log user login activity");
                }
            });
            
            return Task.CompletedTask;
        },
        OnUserInformationReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("=== USER INFO RECEIVED ===");
            logger.LogInformation("UserInfo: {UserInfo}", context.User.RootElement.ToString());
            
            return Task.CompletedTask;
        }
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireEditor", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  // Check for groups claims first
                  context.User.HasClaim("groups", "stwiki-editor") ||
                  context.User.HasClaim("groups", "stwiki-admin") ||
                  // Fallback: check email for known admin/editor accounts
                  context.User.HasClaim(ClaimTypes.Email, "admin@example.com") ||
                  context.User.HasClaim(ClaimTypes.Email, "editor@example.com") ||
                  context.User.HasClaim("email", "admin@example.com") ||
                  context.User.HasClaim("email", "editor@example.com")));
                  
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  // Check for groups claims first
                  context.User.HasClaim("groups", "stwiki-admin") ||
                  // Fallback: check email for known admin account
                  context.User.HasClaim(ClaimTypes.Email, "admin@example.com") ||
                  context.User.HasClaim("email", "admin@example.com")));
});

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Handle trailing slashes by redirecting to non-trailing slash URLs
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null && path.Length > 1 && path.EndsWith('/'))
    {
        // Remove trailing slash and redirect
        var newPath = path.TrimEnd('/');
        var newUrl = $"{context.Request.Scheme}://{context.Request.Host}{newPath}{context.Request.QueryString}";
        context.Response.Redirect(newUrl, permanent: true);
        return;
    }
    
    await next.Invoke();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapHub<STWiki.Hubs.EditHub>("/editHub");
app.MapControllers();

// Default redirect to home page
app.MapGet("/", () => Results.Redirect("/main-page"));

// Handle command-line arguments for admin tasks
if (args.Contains("--populate-hierarchy"))
{
    using var scope = app.Services.CreateScope();
    var hierarchyService = scope.ServiceProvider.GetRequiredService<STWiki.Services.IPageHierarchyService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Starting ParentId population from command line");
    var updatedCount = await hierarchyService.PopulateParentIdsFromSlugsAsync();
    logger.LogInformation("Completed ParentId population. Updated {Count} pages", updatedCount);
    
    return; // Exit without starting the web server
}

app.Run();