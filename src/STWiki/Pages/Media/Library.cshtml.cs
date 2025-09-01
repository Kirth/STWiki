using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using STWiki.Services;

namespace STWiki.Pages.Media;

[Authorize(Policy = "RequireEditor")]
public class LibraryModel : PageModel
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<LibraryModel> _logger;

    public LibraryModel(IMediaService mediaService, ILogger<LibraryModel> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        return Page();
    }
}