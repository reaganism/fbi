using System;
using System.Collections.Generic;
using System.Linq;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Matching;

// TODO: Heavily optimize after rewrite.

internal sealed class FuzzyLineMatcher
{
    public const float DEFAULT_MIN_MATCH_SCORE = 0.5f;

    public int MaxMatchOffset { get; set; } = FuzzyMatchMatrix.DEFAULT_MAX_OFFSET;

    public float MinMatchScore { get; set; } = DEFAULT_MIN_MATCH_SCORE;

    public void MatchLinesByWords(
        int[]                      matches,
        IReadOnlyList<Utf16String> wmLines1,
        IReadOnlyList<Utf16String> wmLines2
    )
    {
        foreach (var (range1, range2) in FuzzyLineMatching.UnmatchedRanges(matches, wmLines2.Count))
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

    public int[] Match(
        IReadOnlyList<Utf16String> pattern,
        IReadOnlyList<Utf16String> search
    )
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

        var mm = new FuzzyMatchMatrix(pattern, search, MaxMatchOffset);
        for (var i = mm.WorkingRange.First; mm.Match(i, out var score); i++)
        {
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestMatch = mm.Path();
        }

        return bestMatch ?? Enumerable.Repeat(-1, pattern.Count).ToArray();
    }

    // Assumes the lines are in word-to-char mode.
    public static float MatchLines(Utf16String s, Utf16String t)
    {
        var d = Utf16String.LevenshteinDistance(s, t);
        if (d == 0)
        {
            // Perfect match!
            return 1f;
        }

        var max = Math.Max(s.Length, t.Length) / 2f;
        return Math.Max(0f, 1f - (d / max));
    }
}