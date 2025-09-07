using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace STWiki.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    public Dictionary<string, object> SystemInfo { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // Get system information
        SystemInfo["ServerTime"] = DateTimeOffset.UtcNow;
        SystemInfo["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown";
        SystemInfo["DotNetVersion"] = Environment.Version.ToString();
        SystemInfo["MachineName"] = Environment.MachineName;
        SystemInfo["ProcessorCount"] = Environment.ProcessorCount;
        SystemInfo["WorkingSet"] = GC.GetTotalMemory(false);
        
        return Page();
    }
}