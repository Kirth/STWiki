using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace STWiki.Services;

public class ClaimsTransformation : IClaimsTransformation
{
    private readonly ILogger<ClaimsTransformation> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ClaimsTransformation(ILogger<ClaimsTransformation> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        var identity = (ClaimsIdentity)principal.Identity;

        // Create or update user record on login and add display name claim
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            var user = await userService.GetOrCreateUserAsync(principal);
            
            // Add the display name as a separate claim for UI display
            if (!string.IsNullOrEmpty(user.DisplayName))
            {
                identity.AddClaim(new Claim("display_name", user.DisplayName));
                _logger.LogDebug("Added display_name claim: {DisplayName}", user.DisplayName);
            }
            
            // Add user slug claim for URL generation
            var userSlug = userService.GetUserSlug(user);
            identity.AddClaim(new Claim("user_slug", userSlug));
            _logger.LogDebug("Added user_slug claim: {UserSlug}", userSlug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update user record during claims transformation");
        }

        // Log all received claims for debugging
        _logger.LogInformation("=== CLAIMS TRANSFORMATION ===");
        foreach (var claim in principal.Claims)
        {
            _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
        }

        // Ensure we have a proper Name claim using the sub claim as the primary identifier
        var subClaim = principal.FindFirst("sub") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
        
        if (subClaim != null)
        {
            // Use the sub claim as the primary identity name for consistent user identification
            if (!principal.HasClaim(ClaimTypes.Name, subClaim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, subClaim.Value));
                _logger.LogInformation("Added Name claim from sub: {Sub}", subClaim.Value);
            }
        }
        else
        {
            // Fallback to email/username if no sub claim (shouldn't happen with proper OIDC)
            var emailClaim = principal.FindFirst(ClaimTypes.Email)
                          ?? principal.FindFirst("email");

            var usernameClaim = principal.FindFirst("preferred_username")
                             ?? principal.FindFirst("username");

            if (emailClaim != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, emailClaim.Value));
                _logger.LogWarning("Using email as Name claim - no sub claim found: {Email}", emailClaim.Value);
            }
            else if (usernameClaim != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, usernameClaim.Value));
                _logger.LogWarning("Using username as Name claim - no sub claim found: {Username}", usernameClaim.Value);
            }
            else
            {
                _logger.LogError("No suitable claim found for user identification");
            }
        }

        // Transform groups claims to the expected format
        var groupsClaims = principal.FindAll("groups").ToList();
        if (groupsClaims.Any())
        {
            foreach (var groupClaim in groupsClaims)
            {
                _logger.LogInformation("Found groups claim: {Group}", groupClaim.Value);
            }
        }
        else
        {
            _logger.LogWarning("No groups claims found!");
            
            // TEMPORARY: Add admin group for testing if user has the known admin sub
            var userSubClaim = principal.FindFirst("sub") ?? principal.FindFirst(ClaimTypes.NameIdentifier);
            var emailClaim = principal.FindFirst(ClaimTypes.Email) ?? principal.FindFirst("email");
            
            // Add admin groups for the known admin user (by email for now, since we're migrating)
            if (emailClaim?.Value == "admin@example.com")
            {
                identity.AddClaim(new Claim("groups", "stwiki-admin"));
                identity.AddClaim(new Claim("groups", "stwiki-editor"));
                _logger.LogInformation("Added temporary admin groups for admin user with sub: {Sub}", userSubClaim?.Value);
            }
        }

        _logger.LogInformation("=== END CLAIMS TRANSFORMATION ===");

        return principal;
    }
}
