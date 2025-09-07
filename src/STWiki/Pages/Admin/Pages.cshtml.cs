using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Data.Entities;
using STWiki.Services;

namespace STWiki.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class PagesModel : PageModel
{
    private readonly AdminService _adminService;

    public PagesModel(AdminService adminService)
    {
        _adminService = adminService;
    }

    public List<STWiki.Data.Entities.Page> Pages { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalPagesCount { get; set; }
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
            Pages = await _adminService.GetAllPagesAsync(CurrentPage, PageSize, SearchTerm);
            TotalPagesCount = await _adminService.GetPageCountAsync(SearchTerm);
            TotalPages = (int)Math.Ceiling((double)TotalPagesCount / PageSize);

            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while loading pages.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostToggleLockAsync(Guid pageId, bool locked)
    {
        try
        {
            var adminUserId = User.Identity?.Name ?? "admin";
            var success = await _adminService.TogglePageLockAsync(pageId, locked, adminUserId);
            
            if (success)
            {
                TempData["SuccessMessage"] = $"Page {(locked ? "locked" : "unlocked")} successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update page lock status.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while updating page lock status.";
        }

        return RedirectToPage(new { Search = SearchTerm, Page = CurrentPage });
    }

    public async Task<IActionResult> OnPostDeletePageAsync(Guid pageId)
    {
        try
        {
            var adminUserId = User.Identity?.Name ?? "admin";
            var success = await _adminService.DeletePageAsync(pageId, adminUserId);
            
            if (success)
            {
                TempData["SuccessMessage"] = "Page deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete page.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "An error occurred while deleting the page.";
        }

        return RedirectToPage(new { Search = SearchTerm, Page = CurrentPage });
    }
}