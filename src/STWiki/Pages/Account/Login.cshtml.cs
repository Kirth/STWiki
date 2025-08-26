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
            return Redirect("/wiki");
        }
        
        return Page();
    }

    public IActionResult OnGetChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/wiki"
        }, "oidc");
    }

    public IActionResult OnPostChallenge()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/wiki"
        }, "oidc");
    }
}