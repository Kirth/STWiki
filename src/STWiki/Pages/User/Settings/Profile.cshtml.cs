using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;
using System.ComponentModel.DataAnnotations;

namespace STWiki.Pages.User.Settings;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly UserService _userService;

    public ProfileModel(UserService userService)
    {
        _userService = userService;
    }

    public STWiki.Data.Entities.User UserProfile { get; set; } = null!;

    [BindProperty]
    [Required(ErrorMessage = "Display name is required")]
    [StringLength(255, ErrorMessage = "Display name cannot exceed 255 characters")]
    public string DisplayName { get; set; } = "";

    [BindProperty]
    [StringLength(1000, ErrorMessage = "Bio cannot exceed 1000 characters")]
    public string? Bio { get; set; } = "";

    [BindProperty]
    [StringLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters")]
    public string? AvatarUrl { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.Name == null)
        {
            return Challenge();
        }

        UserProfile = await _userService.GetOrCreateUserAsync(User);
        DisplayName = UserProfile.DisplayName;
        Bio = UserProfile.Bio ?? "";
        AvatarUrl = UserProfile.AvatarUrl ?? "";

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

            // Additional URL validation for avatar (only if provided and not empty)
            if (!string.IsNullOrWhiteSpace(AvatarUrl))
            {
                if (!Uri.TryCreate(AvatarUrl, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    ModelState.AddModelError("AvatarUrl", "Please enter a valid HTTP or HTTPS URL.");
                    return Page();
                }
            }

            await _userService.UpdateUserProfileAsync(
                UserProfile.Id, 
                DisplayName.Trim(), 
                Bio?.Trim() ?? "", 
                AvatarUrl?.Trim() ?? "");
            
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "An error occurred while updating your profile. Please try again.");
            UserProfile = await _userService.GetOrCreateUserAsync(User);
            return Page();
        }
    }
}