using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STWiki.Pages.Account;

public class LoginModel : PageModel
{
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(GetSafeReturnUrl(returnUrl));
        }
        
        return Page();
    }

    public IActionResult OnGetChallenge(string? returnUrl = null)
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = GetSafeReturnUrl(returnUrl)
        }, "oidc");
    }

    public IActionResult OnPostChallenge(string? returnUrl = null)
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = GetSafeReturnUrl(returnUrl)
        }, "oidc");
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        // Ensure the return URL is safe (local to this application)
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }
        
        // Default fallback
        return "/main-page";
    }
}