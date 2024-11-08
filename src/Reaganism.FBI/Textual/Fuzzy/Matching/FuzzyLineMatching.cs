using System;
using System.Collections.Generic;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Matching;

// TODO: Heavily optimize after rewrite.

internal static class FuzzyLineMatching
{
    public static IEnumerable<(LineRange, LineRange)> UnmatchedRanges(int[] matches, int len2)
    {
        var len1   = matches.Length;
        var start1 = 0;
        var start2 = 0;

        do
        {
            // Search for a matchpoint.
            var end1 = start1;
            while (end1 < len1 && matches[end1] < 0)
            {
                end1++;
            }

            var end2 = end1 == len1 ? len2 : matches[end1];
            if (end1 != start1 || end2 != start2)
            {
                yield return (new LineRange(start1, end1), new LineRange(start2, end2));
                start1 = end1;
                start2 = end2;
            }
            else
            {
                // Matchpoint follows on from the start, no unmatched lines.
                start1++;
                start2++;
            }
        }
        while (start1 < len1 || start2 < len2);
    }

    private static IEnumerable<(LineRange, LineRange)> UnmatchedRanges(
        IEnumerable<FuzzyPatch> patches
    )
    {
        foreach (var patch in patches)
        {
            var diffs  = patch.Diffs;
            var start1 = patch.Start1;
            var start2 = patch.Start2;

            for (var i = 0; i < diffs.Count;)
            {
                // Skip matched.
                while (i < diffs.Count && diffs[i].Operation == FuzzyOperation.EQUALS)
                {
                    start1++;
                    start2++;
                    i++;
                }

                var end1 = start1;
                var end2 = start2;
                while (i < diffs.Count && diffs[i].Operation != FuzzyOperation.EQUALS)
                {
                    if (diffs[i++].Operation == FuzzyOperation.DELETE)
                    {
                        end1++;
                    }
                    else
                    {
                        end2++;
                    }
                }

                if (end1 != start1 || end2 != start2)
                {
                    yield return (new LineRange(start1, end1), new LineRange(start2, end2));
                }

                start1 = end1;
                start2 = end2;
            }
        }
    }

    private static int[] FromUnmatchedRanges(
        IEnumerable<(LineRange, LineRange)> unmatchedRanges,
        int                                 len1
    )
    {
        var matches = new int[len1];
        var start1  = 0;
        var start2  = 0;

        foreach (var (range1, range2) in unmatchedRanges)
        {
            while (start1 < range1.Start)
            {
                matches[start1++] = start2++;
            }

            if (start2 != range2.Start)
            {
                throw new InvalidOperationException("Unequal number of lines between unmatched ranges on each side.");
            }

            while (start1 < range1.End)
            {
                matches[start1++] = -1;
            }

            start2 = range2.End;
        }

        while (start1 < len1)
        {
            matches[start1++] = start2++;
        }

        return matches;
    }

    public static int[] FromPatches(IEnumerable<FuzzyPatch> patches, int len1)
    {
        return FromUnmatchedRanges(UnmatchedRanges(patches), len1);
    }

    public static List<FuzzyDiffLine> MakeDiffList(
        int[]                      matches,
        IReadOnlyList<Utf16String> lines1,
        IReadOnlyList<Utf16String> lines2
    )
    {
        var list = new List<FuzzyDiffLine>(Math.Max(Math.Max(matches.Length, lines1.Count), lines2.Count));

        var l = 0;
        var r = 0;
        for (var i = 0; i < matches.Length; i++)
        {
            if (matches[i] < 0)
            {
                continue;
            }

            while (l < i)
            {
                list.Add(new FuzzyDiffLine(FuzzyOperation.DELETE, lines1[l++]));
            }

            while (r < matches[i])
            {
                list.Add(new FuzzyDiffLine(FuzzyOperation.INSERT, lines2[r++]));
            }

            if (lines1[l] != lines2[r])
            {
                list.Add(new FuzzyDiffLine(FuzzyOperation.DELETE, lines1[l]));
                list.Add(new FuzzyDiffLine(FuzzyOperation.INSERT, lines2[r]));
            }
            else
            {
                list.Add(new FuzzyDiffLine(FuzzyOperation.EQUALS, lines1[l]));
            }

            l++;
            r++;
        }

        while (l < lines1.Count)
        {
            list.Add(new FuzzyDiffLine(FuzzyOperation.DELETE, lines1[l++]));
        }

        while (r < lines2.Count)
        {
            list.Add(new FuzzyDiffLine(FuzzyOperation.INSERT, lines2[r++]));
        }

        return list;
    }
}