using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STWiki.Services;

namespace STWiki.Controllers;

[Authorize(Policy = "RequireAdmin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IPageHierarchyService _pageHierarchyService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IPageHierarchyService pageHierarchyService, ILogger<AdminController> logger)
    {
        _pageHierarchyService = pageHierarchyService;
        _logger = logger;
    }

    [HttpPost("populate-parent-ids")]
    public async Task<IActionResult> PopulateParentIds()
    {
        try
        {
            _logger.LogInformation("Admin initiated ParentId population");
            
            var updatedCount = await _pageHierarchyService.PopulateParentIdsFromSlugsAsync();
            
            return Ok(new 
            { 
                success = true, 
                message = $"Successfully populated ParentId for {updatedCount} pages",
                updatedCount 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating ParentIds");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("validate-hierarchy")]
    public async Task<IActionResult> ValidateHierarchy()
    {
        try
        {
            var isConsistent = await _pageHierarchyService.ValidateHierarchyConsistencyAsync();
            
            return Ok(new 
            { 
                success = true, 
                isConsistent,
                message = isConsistent ? "Hierarchy is consistent" : "Hierarchy inconsistencies detected (check logs)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating hierarchy");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}