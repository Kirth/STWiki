using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using STWiki.Data;
using STWiki.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Add custom services
builder.Services.AddSingleton<STWiki.Services.MarkdownService>();
builder.Services.AddSingleton<STWiki.Services.DiffService>();
builder.Services.AddScoped<STWiki.Services.TemplateService>();
builder.Services.AddScoped<STWiki.Services.IRedirectService, STWiki.Services.RedirectService>();
builder.Services.AddHttpClient();
builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformation>();

// Add authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie("Cookies", options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
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
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapControllers();

// Default redirect to home page
app.MapGet("/", () => Results.Redirect("/main-page"));


app.Run();