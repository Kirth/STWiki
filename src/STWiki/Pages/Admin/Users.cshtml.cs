using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class UsersModel : PageModel
{
    private readonly AdminService _adminService;
    private readonly UserService _userService;

    public UsersModel(AdminService adminService, UserService userService)
    {
        _adminService = adminService;
        _userService = userService;
    }

    public List<STWiki.Data.Entities.User> Users { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalUsers { get; set; }
    public string SearchTerm { get; set; } = "";
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync()
    {
        SearchTerm = Search ?? "";
        CurrentPage = Page;

        try
        {
            Users = await _adminService.GetAllUsersAsync(CurrentPage, PageSize, SearchTerm);
            TotalUsers = await _adminService.GetUserCountAsync(SearchTerm);
            TotalPages = (int)Math.Ceiling((double)TotalUsers / PageSize);

            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while loading users.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostToggleUserProfileAsync(long userId, bool isPublic)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                await _userService.UpdateUserPreferencesAsync(
                    userId, 
                    isPublic, 
                    user.ShowActivityPublic, 
                    user.ShowContributionsPublic, 
                    user.ThemePreference, 
                    user.EmailNotifications);
                
                TempData["SuccessMessage"] = $"User profile visibility updated for {user.DisplayName}.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Failed to update user profile visibility.";
        }

        return RedirectToPage(new { Search = SearchTerm, Page = CurrentPage });
    }
}