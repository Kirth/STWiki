using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace STWiki.Services;

public class ClaimsTransformation : IClaimsTransformation
{
    private readonly ILogger<ClaimsTransformation> _logger;

    public ClaimsTransformation(ILogger<ClaimsTransformation> logger)
    {
        _logger = logger;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return Task.FromResult(principal);

        var identity = (ClaimsIdentity)principal.Identity;

        // Log all received claims for debugging
        // _logger.LogInformation("=== CLAIMS TRANSFORMATION ===");
        foreach (var claim in principal.Claims)
        {
            // _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
        }

        // Ensure we have a proper Name claim
        if (!principal.HasClaim(ClaimTypes.Name, ""))
        {
            var emailClaim = principal.FindFirst(ClaimTypes.Email)
                          ?? principal.FindFirst("email");

            var usernameClaim = principal.FindFirst("preferred_username")
                             ?? principal.FindFirst("username");

            if (emailClaim != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, emailClaim.Value));
                // _logger.LogInformation("Added Name claim from email: {Email}", emailClaim.Value);
            }
            else if (usernameClaim != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, usernameClaim.Value));
                // _logger.LogInformation("Added Name claim from username: {Username}", usernameClaim.Value);
            }
        }

        // Transform groups claims to the expected format
        var groupsClaims = principal.FindAll("groups").ToList();
        if (groupsClaims.Any())
        {
            foreach (var groupClaim in groupsClaims)
            {
                // _logger.LogInformation("Found groups claim: {Group}", groupClaim.Value);
            }
        }
        else
        {
            _logger.LogWarning("No groups claims found!");
        }

        // _logger.LogInformation("=== END CLAIMS TRANSFORMATION ===");

        return Task.FromResult(principal);
    }
}
