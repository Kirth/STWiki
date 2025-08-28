using System.Text.RegularExpressions;

namespace STWiki.Extensions;

public static class RegexExtensions
{
    public static async Task<string> ReplaceAsync(string input, string pattern, Func<Match, Task<string>> replacementFunc)
    {
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var matches = regex.Matches(input).Cast<Match>().ToList();
        
        var result = input;
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var replacement = await replacementFunc(match);
            result = result.Substring(0, match.Index) + replacement + result.Substring(match.Index + match.Length);
        }
        
        return result;
    }
}