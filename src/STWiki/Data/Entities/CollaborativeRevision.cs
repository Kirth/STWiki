using System.ComponentModel.DataAnnotations;

namespace STWiki.Data.Entities;

public class CollaborativeRevision : Revision
{
    [MaxLength(2000)]
    public string Contributors { get; set; } = "";
    
    public int OperationCount { get; set; }
    
    public DateTimeOffset CollaborationStart { get; set; }
    
    public DateTimeOffset CollaborationEnd { get; set; }
    
    public bool IsCollaborative { get; set; } = true;
    
    // Helper method to get contributors as a list
    public List<string> GetContributors()
    {
        if (string.IsNullOrEmpty(Contributors))
            return new List<string>();
            
        return Contributors.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(c => c.Trim())
                          .ToList();
    }
    
    // Helper method to set contributors from a list
    public void SetContributors(IEnumerable<string> contributors)
    {
        Contributors = string.Join(", ", contributors.Distinct());
    }
    
    // Helper method to add a contributor
    public void AddContributor(string contributor)
    {
        if (string.IsNullOrWhiteSpace(contributor))
            return;
            
        var currentContributors = GetContributors();
        if (!currentContributors.Contains(contributor, StringComparer.OrdinalIgnoreCase))
        {
            currentContributors.Add(contributor);
            SetContributors(currentContributors);
        }
    }
}