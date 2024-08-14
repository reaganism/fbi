using System;
using System.Collections.Generic;

namespace Reaganism.FBI.Diffing;

internal static class LineMatching
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

            // Matchpoint follows on from the start, no unmatched lines.
            start1++;
            start2++;
        }
        while (start1 < len1 || start2 < len2);
    }

    private static IEnumerable<(LineRange, LineRange)> UnmatchedRanges(IEnumerable<Patch> patches)
    {
        foreach (var patch in patches)
        {
            var diffs  = patch.Diffs;
            var start1 = patch.Start1;
            var start2 = patch.Start2;

            for (var i = 0; i < diffs.Count;)
            {
                // Skip matched.
                while (i < diffs.Count && diffs[i].Operation == Operation.EQUALS)
                {
                    start1++;
                    start2++;
                    i++;
                }

                var end1 = start1;
                var end2 = start2;
                while (i < diffs.Count && diffs[i].Operation != Operation.EQUALS)
                {
                    if (diffs[i++].Operation == Operation.DELETE)
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

    private static int[] FromUnmatchedRanges(IEnumerable<(LineRange, LineRange)> unmatchedRanges, int len1)
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
                throw new InvalidOperationException("Unequal n umber of lines between unmatched ranges on each side.");
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

    public static int[] FromPatches(IEnumerable<Patch> patches, int len1)
    {
        return FromUnmatchedRanges(UnmatchedRanges(patches), len1);
    }

    public static List<DiffLine> MakeDiffList(int[] matches, IReadOnlyList<string> originalLines, IReadOnlyList<string> modifiedLines)
    {
        var list = new List<DiffLine>();

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
                list.Add(new DiffLine(Operation.DELETE, originalLines[l++]));
            }

            while (r < matches[i])
            {
                list.Add(new DiffLine(Operation.INSERT, modifiedLines[r++]));
            }

            if (originalLines[l] != modifiedLines[r])
            {
                list.Add(new DiffLine(Operation.DELETE, originalLines[l]));
                list.Add(new DiffLine(Operation.INSERT, originalLines[r]));
            }
            else
            {
                list.Add(new DiffLine(Operation.EQUALS, originalLines[l]));
            }

            l++;
            r++;
        }

        while (l < originalLines.Count)
        {
            list.Add(new DiffLine(Operation.DELETE, originalLines[l++]));
        }

        while (r < modifiedLines.Count)
        {
            list.Add(new DiffLine(Operation.INSERT, modifiedLines[r++]));
        }

        return list;
    }
}