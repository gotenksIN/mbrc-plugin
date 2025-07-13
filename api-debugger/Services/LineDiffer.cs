using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicBeeRemote.ApiDebugger.Services;

/// <summary>
/// Represents a segment within a line that may be highlighted differently.
/// </summary>
public class DiffSegment
{
    public string Text { get; set; } = "";
    public bool IsChanged { get; set; }
}

/// <summary>
/// Represents a line in a diff with its status.
/// </summary>
public class DiffLine
{
    public int? LineNumberA { get; set; }
    public int? LineNumberB { get; set; }
    public string Text { get; set; } = "";
    public DiffLineType Type { get; set; }

    /// <summary>
    /// For modified lines, contains segments with change highlighting.
    /// </summary>
    public List<DiffSegment> Segments { get; set; } = [];

    /// <summary>
    /// Whether this line has inline segments (for modified lines).
    /// </summary>
    public bool HasSegments => Segments.Count > 0;
}

/// <summary>
/// Type of diff line.
/// </summary>
public enum DiffLineType
{
    /// <summary>Line is unchanged in both versions.</summary>
    Unchanged,
    /// <summary>Line was added in B (not in A).</summary>
    Added,
    /// <summary>Line was removed from A (not in B).</summary>
    Removed,
    /// <summary>Line was modified (different content but similar structure).</summary>
    Modified
}

/// <summary>
/// Computes line-by-line diffs between two text blocks.
/// Uses a simplified LCS (Longest Common Subsequence) approach with inline diff support.
/// </summary>
public static class LineDiffer
{
    /// <summary>
    /// Computes side-by-side diff lines for display with inline highlighting.
    /// </summary>
    public static (List<DiffLine> Left, List<DiffLine> Right) ComputeSideBySideDiff(string? textA, string? textB)
    {
        var linesA = SplitLines(textA);
        var linesB = SplitLines(textB);

        var left = new List<DiffLine>();
        var right = new List<DiffLine>();

        var lcs = ComputeLcsMatrix(linesA, linesB);
        BuildSideBySideDiff(linesA, linesB, lcs, left, right);

        // Post-process to detect modified lines (similar structure, different values)
        DetectModifiedLines(left, right);

        return (left, right);
    }

    /// <summary>
    /// Computes a unified diff between two text blocks.
    /// </summary>
    public static List<DiffLine> ComputeDiff(string? textA, string? textB)
    {
        var linesA = SplitLines(textA);
        var linesB = SplitLines(textB);

        var result = new List<DiffLine>();
        var lcs = ComputeLcsMatrix(linesA, linesB);
        BuildDiff(linesA, linesB, lcs, result);

        return result;
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        return text.Split(["\r\n", "\n"], StringSplitOptions.None);
    }

    private static int[,] ComputeLcsMatrix(string[] a, string[] b)
    {
        var m = a.Length;
        var n = b.Length;
        var matrix = new int[m + 1, n + 1];

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    matrix[i, j] = matrix[i - 1, j - 1] + 1;
                }
                else
                {
                    matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);
                }
            }
        }

        return matrix;
    }

    private static void BuildDiff(string[] a, string[] b, int[,] lcs, List<DiffLine> result)
    {
        var i = a.Length;
        var j = b.Length;
        var tempResult = new List<DiffLine>();

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
            {
                tempResult.Add(new DiffLine
                {
                    LineNumberA = i,
                    LineNumberB = j,
                    Text = a[i - 1],
                    Type = DiffLineType.Unchanged
                });
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
            {
                tempResult.Add(new DiffLine
                {
                    LineNumberB = j,
                    Text = b[j - 1],
                    Type = DiffLineType.Added
                });
                j--;
            }
            else if (i > 0)
            {
                tempResult.Add(new DiffLine
                {
                    LineNumberA = i,
                    Text = a[i - 1],
                    Type = DiffLineType.Removed
                });
                i--;
            }
        }

        tempResult.Reverse();
        result.AddRange(tempResult);
    }

    private static void BuildSideBySideDiff(string[] a, string[] b, int[,] lcs,
        List<DiffLine> left, List<DiffLine> right)
    {
        var i = a.Length;
        var j = b.Length;
        var tempLeft = new List<DiffLine>();
        var tempRight = new List<DiffLine>();

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && a[i - 1] == b[j - 1])
            {
                // Unchanged - add to both sides
                tempLeft.Add(new DiffLine
                {
                    LineNumberA = i,
                    Text = a[i - 1],
                    Type = DiffLineType.Unchanged
                });
                tempRight.Add(new DiffLine
                {
                    LineNumberB = j,
                    Text = b[j - 1],
                    Type = DiffLineType.Unchanged
                });
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
            {
                // Added in B - blank on left, content on right
                tempLeft.Add(new DiffLine
                {
                    Text = "",
                    Type = DiffLineType.Added
                });
                tempRight.Add(new DiffLine
                {
                    LineNumberB = j,
                    Text = b[j - 1],
                    Type = DiffLineType.Added
                });
                j--;
            }
            else if (i > 0)
            {
                // Removed from A - content on left, blank on right
                tempLeft.Add(new DiffLine
                {
                    LineNumberA = i,
                    Text = a[i - 1],
                    Type = DiffLineType.Removed
                });
                tempRight.Add(new DiffLine
                {
                    Text = "",
                    Type = DiffLineType.Removed
                });
                i--;
            }
        }

        tempLeft.Reverse();
        tempRight.Reverse();
        left.AddRange(tempLeft);
        right.AddRange(tempRight);
    }

    /// <summary>
    /// Detects pairs of removed/added lines that are actually modifications
    /// and computes inline character-level diffs.
    /// </summary>
    private static void DetectModifiedLines(List<DiffLine> left, List<DiffLine> right)
    {
        for (var i = 0; i < left.Count; i++)
        {
            var leftLine = left[i];
            var rightLine = right[i];

            // Look for adjacent removed/added pairs that might be modifications
            if (leftLine.Type == DiffLineType.Removed &&
                rightLine.Type == DiffLineType.Added &&
                !string.IsNullOrEmpty(leftLine.Text) &&
                !string.IsNullOrEmpty(rightLine.Text))
            {
                // Check if the lines are similar (e.g., same JSON key, different value)
                if (AreLinesModified(leftLine.Text, rightLine.Text))
                {
                    leftLine.Type = DiffLineType.Modified;
                    rightLine.Type = DiffLineType.Modified;

                    // Compute inline diff segments
                    ComputeInlineSegments(leftLine, rightLine);
                }
            }
        }
    }

    /// <summary>
    /// Determines if two lines are likely modifications of each other.
    /// </summary>
    private static bool AreLinesModified(string lineA, string lineB)
    {
        // For JSON: same key, different value
        var trimA = lineA.TrimStart();
        var trimB = lineB.TrimStart();

        // Check if both lines start with the same JSON key
        if (trimA.StartsWith('"') && trimB.StartsWith('"'))
        {
            var colonIndexA = trimA.IndexOf(':');
            var colonIndexB = trimB.IndexOf(':');

            if (colonIndexA > 0 && colonIndexB > 0)
            {
                var keyA = trimA[..colonIndexA];
                var keyB = trimB[..colonIndexB];

                if (keyA == keyB)
                {
                    return true;
                }
            }
        }

        // Check similarity ratio for non-JSON lines
        var similarity = ComputeSimilarity(lineA, lineB);
        return similarity > 0.5; // More than 50% similar
    }

    /// <summary>
    /// Computes inline diff segments for modified lines.
    /// </summary>
    private static void ComputeInlineSegments(DiffLine leftLine, DiffLine rightLine)
    {
        var textA = leftLine.Text;
        var textB = rightLine.Text;

        // Find common prefix
        var prefixLen = 0;
        var minLen = Math.Min(textA.Length, textB.Length);
        while (prefixLen < minLen && textA[prefixLen] == textB[prefixLen])
        {
            prefixLen++;
        }

        // Find common suffix (but don't overlap with prefix)
        var suffixLen = 0;
        while (suffixLen < minLen - prefixLen &&
               textA[textA.Length - 1 - suffixLen] == textB[textB.Length - 1 - suffixLen])
        {
            suffixLen++;
        }

        // Build segments for left (A)
        if (prefixLen > 0)
        {
            leftLine.Segments.Add(new DiffSegment
            {
                Text = textA[..prefixLen],
                IsChanged = false
            });
        }

        var middleStartA = prefixLen;
        var middleEndA = textA.Length - suffixLen;
        if (middleEndA > middleStartA)
        {
            leftLine.Segments.Add(new DiffSegment
            {
                Text = textA[middleStartA..middleEndA],
                IsChanged = true
            });
        }

        if (suffixLen > 0)
        {
            leftLine.Segments.Add(new DiffSegment
            {
                Text = textA[^suffixLen..],
                IsChanged = false
            });
        }

        // Build segments for right (B)
        if (prefixLen > 0)
        {
            rightLine.Segments.Add(new DiffSegment
            {
                Text = textB[..prefixLen],
                IsChanged = false
            });
        }

        var middleStartB = prefixLen;
        var middleEndB = textB.Length - suffixLen;
        if (middleEndB > middleStartB)
        {
            rightLine.Segments.Add(new DiffSegment
            {
                Text = textB[middleStartB..middleEndB],
                IsChanged = true
            });
        }

        if (suffixLen > 0)
        {
            rightLine.Segments.Add(new DiffSegment
            {
                Text = textB[^suffixLen..],
                IsChanged = false
            });
        }
    }

    /// <summary>
    /// Computes similarity ratio between two strings (0.0 to 1.0).
    /// </summary>
    private static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0.0;

        var maxLen = Math.Max(a.Length, b.Length);
        var distance = ComputeLevenshteinDistance(a, b);

        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Computes Levenshtein edit distance between two strings.
    /// </summary>
    private static int ComputeLevenshteinDistance(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
            dp[i, 0] = i;
        for (var j = 0; j <= n; j++)
            dp[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }

    /// <summary>
    /// Gets statistics about the diff.
    /// </summary>
    public static DiffStats GetStats(List<DiffLine> diff)
    {
        return new DiffStats
        {
            TotalLines = diff.Count,
            UnchangedLines = diff.Count(d => d.Type == DiffLineType.Unchanged),
            AddedLines = diff.Count(d => d.Type == DiffLineType.Added),
            RemovedLines = diff.Count(d => d.Type == DiffLineType.Removed),
            ModifiedLines = diff.Count(d => d.Type == DiffLineType.Modified)
        };
    }
}

/// <summary>
/// Statistics about a diff.
/// </summary>
public class DiffStats
{
    public int TotalLines { get; set; }
    public int UnchangedLines { get; set; }
    public int AddedLines { get; set; }
    public int RemovedLines { get; set; }
    public int ModifiedLines { get; set; }
    public bool HasChanges => AddedLines > 0 || RemovedLines > 0 || ModifiedLines > 0;
}
