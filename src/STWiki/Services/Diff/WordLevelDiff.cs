using System.Text;
using System.Text.RegularExpressions;

namespace STWiki.Services.Diff;

public class WordLevelDiff
{
    public class WordDiffResult
    {
        public List<WordDiffItem> Items { get; set; } = new();
        public int AddedWords { get; set; }
        public int DeletedWords { get; set; }
        public int ModifiedLines { get; set; }
    }

    public class WordDiffItem
    {
        public WordDiffOperationType Operation { get; set; }
        public string Text { get; set; } = "";
        public int LineNumber { get; set; }
        public bool IsWhitespace { get; set; }
    }

    public enum WordDiffOperationType
    {
        Equal,
        Delete,
        Insert,
        Context
    }

    private static readonly Regex WordBoundaryRegex = new(@"\b|\s+", RegexOptions.Compiled);

    public static WordDiffResult ComputeWordDiff(string oldText, string newText, int contextWords = 3)
    {
        var oldLines = SplitIntoLines(oldText);
        var newLines = SplitIntoLines(newText);

        var result = new WordDiffResult();
        var lineDiff = MyersDiffAlgorithm.Compute(oldLines.ToArray(), newLines.ToArray());

        var items = new List<WordDiffItem>();

        foreach (var lineItem in lineDiff)
        {
            switch (lineItem.Operation)
            {
                case MyersDiffAlgorithm.DiffOperationType.Equal:
                    var words = SplitIntoWords(lineItem.Text, lineItem.LineNumber);
                    items.AddRange(words.Select(w => new WordDiffItem
                    {
                        Operation = WordDiffOperationType.Equal,
                        Text = w.Text,
                        LineNumber = w.LineNumber,
                        IsWhitespace = w.IsWhitespace
                    }));
                    break;

                case MyersDiffAlgorithm.DiffOperationType.Delete:
                    var deletedWords = SplitIntoWords(lineItem.Text, lineItem.LineNumber);
                    items.AddRange(deletedWords.Select(w => new WordDiffItem
                    {
                        Operation = WordDiffOperationType.Delete,
                        Text = w.Text,
                        LineNumber = w.LineNumber,
                        IsWhitespace = w.IsWhitespace
                    }));
                    result.DeletedWords += deletedWords.Count(w => !w.IsWhitespace);
                    break;

                case MyersDiffAlgorithm.DiffOperationType.Insert:
                    var insertedWords = SplitIntoWords(lineItem.Text, lineItem.LineNumber);
                    items.AddRange(insertedWords.Select(w => new WordDiffItem
                    {
                        Operation = WordDiffOperationType.Insert,
                        Text = w.Text,
                        LineNumber = w.LineNumber,
                        IsWhitespace = w.IsWhitespace
                    }));
                    result.AddedWords += insertedWords.Count(w => !w.IsWhitespace);
                    break;
            }
        }

        result.Items = OptimizeWordDiff(items, contextWords);
        result.ModifiedLines = CountModifiedLines(result.Items);

        return result;
    }

    public static WordDiffResult ComputeIntraLineDiff(string oldLine, string newLine, int lineNumber = 1)
    {
        var oldWords = SplitIntoWords(oldLine, lineNumber);
        var newWords = SplitIntoWords(newLine, lineNumber);

        var wordDiff = MyersDiffAlgorithm.Compute(
            oldWords.Select(w => w.Text).ToArray(),
            newWords.Select(w => w.Text).ToArray()
        );

        var result = new WordDiffResult();
        var items = new List<WordDiffItem>();

        foreach (var item in wordDiff)
        {
            var operation = item.Operation switch
            {
                MyersDiffAlgorithm.DiffOperationType.Equal => WordDiffOperationType.Equal,
                MyersDiffAlgorithm.DiffOperationType.Delete => WordDiffOperationType.Delete,
                MyersDiffAlgorithm.DiffOperationType.Insert => WordDiffOperationType.Insert,
                _ => WordDiffOperationType.Equal
            };

            var isWhitespace = string.IsNullOrWhiteSpace(item.Text) || WordBoundaryRegex.IsMatch(item.Text);
            
            items.Add(new WordDiffItem
            {
                Operation = operation,
                Text = item.Text,
                LineNumber = lineNumber,
                IsWhitespace = isWhitespace
            });

            if (operation == WordDiffOperationType.Delete && !isWhitespace)
                result.DeletedWords++;
            else if (operation == WordDiffOperationType.Insert && !isWhitespace)
                result.AddedWords++;
        }

        result.Items = items;
        result.ModifiedLines = items.Any(i => i.Operation != WordDiffOperationType.Equal) ? 1 : 0;

        return result;
    }

    private static List<WordToken> SplitIntoWords(string text, int lineNumber)
    {
        var words = new List<WordToken>();
        var matches = WordBoundaryRegex.Split(text);

        foreach (var match in matches)
        {
            if (string.IsNullOrEmpty(match)) continue;

            words.Add(new WordToken
            {
                Text = match,
                LineNumber = lineNumber,
                IsWhitespace = string.IsNullOrWhiteSpace(match)
            });
        }

        return words;
    }

    private static List<string> SplitIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
    }

    private static List<WordDiffItem> OptimizeWordDiff(List<WordDiffItem> items, int contextWords)
    {
        var optimized = new List<WordDiffItem>();
        var currentSequence = new List<WordDiffItem>();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item.Operation == WordDiffOperationType.Equal)
            {
                if (currentSequence.Any())
                {
                    optimized.AddRange(currentSequence);
                    currentSequence.Clear();
                }

                var contextBefore = Math.Min(contextWords, optimized.Count);
                var contextAfter = Math.Min(contextWords, items.Count - i - 1);

                for (int j = Math.Max(0, optimized.Count - contextBefore); j < optimized.Count; j++)
                {
                    if (optimized[j].Operation == WordDiffOperationType.Context)
                        continue;
                    optimized[j] = new WordDiffItem
                    {
                        Operation = WordDiffOperationType.Context,
                        Text = optimized[j].Text,
                        LineNumber = optimized[j].LineNumber,
                        IsWhitespace = optimized[j].IsWhitespace
                    };
                }

                optimized.Add(item);

                for (int j = 1; j <= contextAfter && i + j < items.Count; j++)
                {
                    var contextItem = items[i + j];
                    if (contextItem.Operation == WordDiffOperationType.Equal)
                    {
                        optimized.Add(new WordDiffItem
                        {
                            Operation = WordDiffOperationType.Context,
                            Text = contextItem.Text,
                            LineNumber = contextItem.LineNumber,
                            IsWhitespace = contextItem.IsWhitespace
                        });
                    }
                }
            }
            else
            {
                currentSequence.Add(item);
            }
        }

        if (currentSequence.Any())
        {
            optimized.AddRange(currentSequence);
        }

        return optimized;
    }

    private static int CountModifiedLines(List<WordDiffItem> items)
    {
        return items
            .Where(i => i.Operation == WordDiffOperationType.Delete || i.Operation == WordDiffOperationType.Insert)
            .Select(i => i.LineNumber)
            .Distinct()
            .Count();
    }

    private class WordToken
    {
        public string Text { get; set; } = "";
        public int LineNumber { get; set; }
        public bool IsWhitespace { get; set; }
    }
}