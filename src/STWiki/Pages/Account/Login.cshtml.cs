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
            return Redirect("/home");
        }
        
        return Page();
    }

    public IActionResult OnGetChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/home"
        }, "oidc");
    }

    public IActionResult OnPostChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/home"
        }, "oidc");
    }
}