using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.User.Settings;

[Authorize]
public class PreferencesModel : PageModel
{
    private readonly UserService _userService;

    public PreferencesModel(UserService userService)
    {
        _userService = userService;
    }

    public STWiki.Data.Entities.User UserProfile { get; set; } = null!;

    [BindProperty]
    public bool IsProfilePublic { get; set; } = true;

    [BindProperty]
    public bool ShowActivityPublic { get; set; } = true;

    [BindProperty]
    public bool ShowContributionsPublic { get; set; } = true;

    [BindProperty]
    public string ThemePreference { get; set; } = "auto";

    [BindProperty]
    public bool EmailNotifications { get; set; } = true;

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.Name == null)
        {
            return Challenge();
        }

        UserProfile = await _userService.GetOrCreateUserAsync(User);
        
        IsProfilePublic = UserProfile.IsProfilePublic;
        ShowActivityPublic = UserProfile.ShowActivityPublic;
        ShowContributionsPublic = UserProfile.ShowContributionsPublic;
        ThemePreference = UserProfile.ThemePreference;
        EmailNotifications = UserProfile.EmailNotifications;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.Name == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            UserProfile = await _userService.GetOrCreateUserAsync(User);
            return Page();
        }

        try
        {
            UserProfile = await _userService.GetOrCreateUserAsync(User);
            
            // Validate theme preference
            var validThemes = new[] { "auto", "light", "dark" };
            if (!validThemes.Contains(ThemePreference))
            {
                ThemePreference = "auto";
            }

            await _userService.UpdateUserPreferencesAsync(
                UserProfile.Id, 
                IsProfilePublic, 
                ShowActivityPublic, 
                ShowContributionsPublic, 
                ThemePreference, 
                EmailNotifications);
            
            TempData["SuccessMessage"] = "Preferences updated successfully!";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while updating your preferences. Please try again.");
            UserProfile = await _userService.GetOrCreateUserAsync(User);
            return Page();
        }
    }
}