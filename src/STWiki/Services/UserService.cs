using Microsoft.EntityFrameworkCore;
using STWiki.Data;
using STWiki.Data.Entities;
using System.Security.Claims;

namespace STWiki.Services;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User> GetOrCreateUserAsync(ClaimsPrincipal claimsPrincipal)
    {
        // Use the sub claim as the primary identifier (this should be the ClaimTypes.Name after transformation)
        var subClaim = claimsPrincipal.FindFirst("sub") ?? claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier);
        var userId = subClaim?.Value ?? claimsPrincipal.Identity?.Name ?? "";
        
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Unable to determine user ID from claims - no sub claim found");
        }

        var email = claimsPrincipal.FindFirst(ClaimTypes.Email)?.Value ?? 
                    claimsPrincipal.FindFirst("email")?.Value ?? "";
        var preferredUsername = claimsPrincipal.FindFirst("preferred_username")?.Value ?? "";
        var displayName = claimsPrincipal.FindFirst("name")?.Value ?? 
                          preferredUsername ?? 
                          email ?? 
                          "User";

        _logger.LogDebug("Looking for user with sub: {UserId}, email: {Email}", userId, email);

        // First, try to find user by the sub claim (new way)
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        
        // Migration fallback: if not found by sub, try to find by email and update the UserId
        if (user == null && !string.IsNullOrEmpty(email))
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && string.IsNullOrEmpty(u.UserId));
            if (user != null)
            {
                _logger.LogInformation("Found existing user by email, updating UserId from empty to sub: {Email} -> {Sub}", email, userId);
                user.UserId = userId;
            }
        }

        if (user == null)
        {
            user = new User
            {
                UserId = userId, // This is now the sub claim
                Email = email,
                DisplayName = displayName,
                PreferredUsername = preferredUsername,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow
            };

            _context.Users.Add(user);
            _logger.LogInformation("Created new user profile with sub: {UserId} for email: {Email}", userId, email);
        }
        else
        {
            // Update user info from claims on each login
            user.Email = email;
            user.PreferredUsername = preferredUsername;
            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            // Only update display name if it's empty or the user hasn't customized it
            if (string.IsNullOrEmpty(user.DisplayName) || user.DisplayName == user.Email || user.DisplayName == user.UserId)
            {
                user.DisplayName = displayName;
            }

            _logger.LogDebug("Updated user profile for sub: {UserId} ({Email})", userId, email);
        }

        try
        {
            await _context.SaveChangesAsync();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user profile for {UserId}. Attempting to retrieve existing user.", userId);
            
            // If save failed, try to get the existing user (might be a race condition)
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (existingUser != null)
            {
                return existingUser;
            }
            
            // If we still can't find the user, re-throw the exception
            throw;
        }
    }

    public async Task<User?> GetUserByUserIdAsync(string userId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<User?> GetUserByIdentifierAsync(string identifier)
    {
        // Try to find user by various identifiers
        // Priority: PreferredUsername -> DisplayName -> UserId (sub) -> Email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => 
                u.PreferredUsername == identifier ||
                u.DisplayName == identifier ||
                u.UserId == identifier ||
                u.Email == identifier);
                
        return user;
    }

    public string GetUserSlug(User user)
    {
        // Return the preferred identifier for URLs
        // Priority: PreferredUsername -> DisplayName -> UserId (sub)
        if (!string.IsNullOrEmpty(user.PreferredUsername))
            return user.PreferredUsername;
        if (!string.IsNullOrEmpty(user.DisplayName))
            return user.DisplayName;
        return user.UserId;
    }

    public async Task<string> GetCurrentUserSlugAsync(ClaimsPrincipal claimsPrincipal)
    {
        var user = await GetOrCreateUserAsync(claimsPrincipal);
        return GetUserSlug(user);
    }

    public async Task<User?> GetUserByIdAsync(long id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetPublicUserProfileAsync(string userId)
    {
        var user = await _context.Users
            .Where(u => u.UserId == userId && u.IsProfilePublic)
            .FirstOrDefaultAsync();

        return user;
    }

    public async Task UpdateUserProfileAsync(long userId, string displayName, string bio, string avatarUrl = "")
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.DisplayName = displayName;
            user.Bio = bio;
            user.AvatarUrl = avatarUrl;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated profile for user {UserId}", user.UserId);
        }
    }

    public async Task UpdateUserPreferencesAsync(long userId, bool isProfilePublic, bool showActivityPublic, 
        bool showContributionsPublic, string themePreference, bool emailNotifications)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.IsProfilePublic = isProfilePublic;
            user.ShowActivityPublic = showActivityPublic;
            user.ShowContributionsPublic = showContributionsPublic;
            user.ThemePreference = themePreference;
            user.EmailNotifications = emailNotifications;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated preferences for user {UserId}", user.UserId);
        }
    }

    public async Task<IList<User>> GetAllUsersAsync(int page = 1, int pageSize = 50)
    {
        return await _context.Users
            .OrderByDescending(u => u.LastLoginAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetUserCountAsync()
    {
        return await _context.Users.CountAsync();
    }

    public async Task<Dictionary<string, object>> GetUserStatsAsync(string userId)
    {
        var user = await GetUserByUserIdAsync(userId);
        if (user == null) return new Dictionary<string, object>();

        var totalActivities = await _context.Activities.CountAsync(a => a.UserId == userId);
        var pagesCreated = await _context.Activities.CountAsync(a => a.UserId == userId && a.ActivityType == ActivityTypes.PageCreated);
        var pagesUpdated = await _context.Activities.CountAsync(a => a.UserId == userId && a.ActivityType == ActivityTypes.PageUpdated);
        
        var firstActivity = await _context.Activities
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        return new Dictionary<string, object>
        {
            ["TotalActivities"] = totalActivities,
            ["PagesCreated"] = pagesCreated,
            ["PagesUpdated"] = pagesUpdated,
            ["FirstActivityDate"] = firstActivity?.CreatedAt,
            ["MemberSince"] = user.CreatedAt,
            ["LastLogin"] = user.LastLoginAt
        };
    }
}