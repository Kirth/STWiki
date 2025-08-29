using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STWiki.Pages.Account;

public class LoginModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/main-page");
        }
        
        return Page();
    }

    public IActionResult OnGetChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/main-page"
        }, "oidc");
    }

    public IActionResult OnPostChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/main-page"
        }, "oidc");
    }
}