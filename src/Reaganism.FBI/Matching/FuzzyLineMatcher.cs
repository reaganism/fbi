using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Diffing;
using Reaganism.FBI.Utilities.Extensions;

namespace Reaganism.FBI.Matching;

[PublicAPI]
public sealed class FuzzyLineMatcher
{
    [PublicAPI]
    public const float DEFAULT_MIN_MATCH_SCORE = 0.5f;

    [PublicAPI]
    public int MaxMatchOffset { [PublicAPI] get; [PublicAPI] set; } = MatchMatrix.DEFAULT_MAX_OFFSET;

    [PublicAPI]
    public float MinMatchScore { [PublicAPI] get; [PublicAPI] set; } = DEFAULT_MIN_MATCH_SCORE;

    [PublicAPI]
    public void MatchLinesByWords(int[] matches, IReadOnlyList<string> wmLines1, IReadOnlyList<string> wmLines2)
    {
        foreach (var (range1, range2) in LineMatching.UnmatchedRanges(matches, wmLines2.Count))
        {
            if (range1.Length == 0 || range2.Length == 0)
            {
                continue;
            }

            var match = Match(wmLines1.Slice(range1), wmLines2.Slice(range2));
            for (var i = 0; i < match.Length; i++)
            {
                if (match[i] >= 0)
                {
                    matches[range1.Start + i] = range2.Start + match[i];
                }
            }
        }
    }

    [PublicAPI]
    public int[] Match(IReadOnlyList<string> pattern, IReadOnlyList<string> search)
    {
        if (search.Count < pattern.Count)
        {
            var rMatch = Match(search, pattern);
            var nMatch = new int[pattern.Count];

            for (var i = 0; i < nMatch.Length; i++)
            {
                nMatch[i] = -1;
            }

            for (var i = 0; i < rMatch.Length; i++)
            {
                if (rMatch[i] >= 0)
                {
                    nMatch[rMatch[i]] = i;
                }
            }

            return nMatch;
        }

        if (pattern.Count == 0)
        {
            return [];
        }

        var bestScore = MinMatchScore;
        var bestMatch = default(int[]);

        var mm = new MatchMatrix(pattern, search, MaxMatchOffset);
        for (var i = mm.WorkingRange.First; mm.Match(i, out var score); i++)
        {
            if (!(score > bestScore))
            {
                continue;
            }

            bestScore = score;
            bestMatch = mm.Path();
        }

        return bestMatch ?? Enumerable.Repeat(-1, pattern.Count).ToArray();
    }

    // Assumes the lines are in word-to-char mode.
    [PublicAPI]
    public static float MatchLines(string s, string t)
    {
        var d = LevenshteinDistance(s, t);
        if (d == 0)
        {
            // Perfect match.
            return 1f;
        }

        var max = Math.Max(s.Length, t.Length) / 2f;
        return Math.Max(0f, 1f - d / max);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        // Degenerate cases.
        if (s == t)
        {
            return 0;
        }

        if (s.Length == 0)
        {
            return t.Length;
        }

        if (s.Length == 0)
        {
            return s.Length;
        }

        // Create two work vectors of integer distances.
        var v0 = new int[t.Length + 1]; // Previous
        var v1 = new int[t.Length + 1]; // Current

        // Initialize v1 (the current row of distances).  This row is
        // A[0][i]: edit distance for an empty `s`.  The distance is just
        // the number of characters to delete from `t`.
        for (var i = 0; i < v1.Length; i++)
        {
            v1[i] = i;
        }

        for (var i = 0; i < s.Length; i++)
        {
            // Swap v1 to v0, reuse old v0 as new v1.
            (v0, v1) = (v1, v0);

            // Calculate v1 (current row distances) from the previous row
            // v0.

            // First element of v1 is A[i + 1][0].  Edit distance is delete
            // (i + 1) chars from `s` to match empty `t`.
            v1[0] = i + 1;

            // Use formulate to fill in the rest of the row.
            for (var j = 0; j < t.Length; j++)
            {
                var del = v0[j + 1] + 1;
                var ins = v1[j]     + 1;
                var sub = v0[j]     + (s[i] == t[j] ? 0 : 1);
                v1[j + 1] = Math.Min(del, Math.Min(ins, sub));
            }
        }

        return v1[t.Length];
    }
}