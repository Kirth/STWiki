using System.Text;

namespace STWiki.Services.Diff;

public class MyersDiffAlgorithm
{
    public class DiffItem
    {
        public DiffOperationType Operation { get; set; }
        public string Text { get; set; } = "";
        public int LineNumber { get; set; }
        public int Position { get; set; }
    }

    public enum DiffOperationType
    {
        Equal,
        Delete,
        Insert
    }

    public static List<DiffItem> Compute(string[] oldLines, string[] newLines)
    {
        var n = oldLines.Length;
        var m = newLines.Length;
        var max = n + m;
        var v = new Dictionary<int, int>();
        var trace = new List<Dictionary<int, int>>();

        v[1] = 0;
        for (var d = 0; d <= max; d++)
        {
            trace.Add(new Dictionary<int, int>(v));
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v.GetValueOrDefault(k - 1, 0) < v.GetValueOrDefault(k + 1, 0)))
                {
                    x = v.GetValueOrDefault(k + 1, 0);
                }
                else
                {
                    x = v.GetValueOrDefault(k - 1, 0) + 1;
                }

                var y = x - k;

                while (x < n && y < m && oldLines[x].Equals(newLines[y]))
                {
                    x++;
                    y++;
                }

                v[k] = x;

                if (x >= n && y >= m)
                {
                    return BacktrackDiff(trace, oldLines, newLines, x, y);
                }
            }
        }

        return new List<DiffItem>();
    }

    private static List<DiffItem> BacktrackDiff(List<Dictionary<int, int>> trace, string[] oldLines, string[] newLines, int x, int y)
    {
        var result = new List<DiffItem>();
        var n = oldLines.Length;
        var m = newLines.Length;

        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;

            int prevK;
            if (k == -d || (k != d && v.GetValueOrDefault(k - 1, 0) < v.GetValueOrDefault(k + 1, 0)))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }

            var prevX = v.GetValueOrDefault(prevK, 0);
            var prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                result.Insert(0, new DiffItem
                {
                    Operation = DiffOperationType.Equal,
                    Text = oldLines[x - 1],
                    LineNumber = x,
                    Position = x - 1
                });
                x--;
                y--;
            }

            if (d > 0)
            {
                if (x > prevX)
                {
                    result.Insert(0, new DiffItem
                    {
                        Operation = DiffOperationType.Delete,
                        Text = oldLines[x - 1],
                        LineNumber = x,
                        Position = x - 1
                    });
                    x--;
                }
                else if (y > prevY)
                {
                    result.Insert(0, new DiffItem
                    {
                        Operation = DiffOperationType.Insert,
                        Text = newLines[y - 1],
                        LineNumber = y,
                        Position = y - 1
                    });
                    y--;
                }
            }
        }

        return result;
    }

    public static List<DiffItem> ComputeWordsFromLines(string[] oldLines, string[] newLines)
    {
        var oldWords = new List<string>();
        var newWords = new List<string>();
        var oldLineMap = new List<int>();
        var newLineMap = new List<int>();

        for (int i = 0; i < oldLines.Length; i++)
        {
            var words = SplitIntoWords(oldLines[i]);
            oldWords.AddRange(words);
            for (int j = 0; j < words.Count; j++)
            {
                oldLineMap.Add(i);
            }
        }

        for (int i = 0; i < newLines.Length; i++)
        {
            var words = SplitIntoWords(newLines[i]);
            newWords.AddRange(words);
            for (int j = 0; j < words.Count; j++)
            {
                newLineMap.Add(i);
            }
        }

        var wordDiff = Compute(oldWords.ToArray(), newWords.ToArray());

        for (int i = 0; i < wordDiff.Count; i++)
        {
            var item = wordDiff[i];
            if (item.Operation == DiffOperationType.Delete && i < oldLineMap.Count)
            {
                item.LineNumber = oldLineMap[item.Position] + 1;
            }
            else if (item.Operation == DiffOperationType.Insert && i < newLineMap.Count)
            {
                item.LineNumber = newLineMap[item.Position] + 1;
            }
        }

        return wordDiff;
    }

    private static List<string> SplitIntoWords(string line)
    {
        var words = new List<string>();
        var currentWord = new StringBuilder();
        var inWord = false;

        foreach (char c in line)
        {
            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    words.Add(currentWord.ToString());
                    currentWord.Clear();
                    inWord = false;
                }
                words.Add(c.ToString());
            }
            else
            {
                currentWord.Append(c);
                inWord = true;
            }
        }

        if (currentWord.Length > 0)
        {
            words.Add(currentWord.ToString());
        }

        return words;
    }
}