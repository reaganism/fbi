using System;
using System.Collections.Generic;
using System.Linq;

using Reaganism.FBI.Textual.Fuzzy.Matching;
using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Diffing;

public static class PatienceDiffer
{
    private static class PatienceMatch
    {
        private class LcaNode(int value, LcaNode? previous)
        {
            public int Value { get; } = value;

            public LcaNode? Previous { get; } = previous;
        }

        public static unsafe int[] Match(ushort[] originalChars, ushort[] modifiedChars, int maxChar)
        {
            var unique1 = (Span<int>)stackalloc int[maxChar];
            var unique2 = (Span<int>)stackalloc int[maxChar];

            for (var i = 0; i < maxChar; i++)
            {
                unique1[i] = unique2[i] = -1;
            }

            return Match(unique1, unique2, originalChars, modifiedChars);
        }

        private static int[] Match(Span<int> unique1, Span<int> unique2, ushort[] chars1, ushort[] chars2)
        {
            var matches = new int[chars1.Length];
            for (var i = 0; i < chars1.Length; i++)
            {
                matches[i] = -1;
            }

            Match(0, chars1.Length, 0, chars2.Length, unique1, unique2, chars1, chars2, matches);
            return matches;
        }

        private static void Match(int start1, int end1, int start2, int end2, Span<int> unique1, Span<int> unique2, ushort[] chars1, ushort[] chars2, int[] matches)
        {
            // Step 1: Match up identical starting lines.
            while (start1 < end1 && start2 < end2 && chars1[start1] == chars2[start2])
            {
                matches[start1++] = start2++;
            }

            // Step 2: Match up identical ending lines.
            while (start1 < end1 && start2 < end2 && chars1[end1 - 1] == chars2[end2 - 1])
            {
                matches[--end1] = --end2;
            }

            // No lines on a side.                  Either a 1-2 or 2-1 which
            //                                      would've been matched by
            //                                      steps 1 and 2.
            if (start1 == end1 || start2 == end2 || end1 - start1 + end2 - start2 <= 3)
            {
                return;
            }

            // Step 3: Match up common unique lines.
            var any = false;
            foreach (var (m1, m2) in LcsUnique(start1, end1, start2, end2, unique1, unique2, chars1, chars2))
            {
                matches[m1] = m2;
                any         = true;

                // Step 4: Recurse.
                Match(start1, m1, start2, m2, unique1, unique2, chars1, chars2, matches);

                start1 = m1 + 1;
                start2 = m2 + 1;
            }

            if (any)
            {
                // ReSharper disable once TailRecursiveCall
                Match(start1, end1, start2, end2, unique1, unique2, chars1, chars2, matches);
            }
        }

        private static IEnumerable<(int, int)> LcsUnique(int start1, int end1, int start2, int end2, Span<int> unique1, Span<int> unique2, ushort[] chars1, ushort[] chars2)
        {
            var subChars = new List<int>();

            // Identify all the unique characters in chars1.
            for (var i = start1; i < end1; i++)
            {
                var c = chars1[i];

                if (unique1[c] == -1)
                {
                    // No lines.
                    unique1[c] = i;
                    subChars.Add(c);
                }
                else
                {
                    // Not unique.
                    unique1[c] = -2;
                }
            }

            // Identify all the unique characters in chars2, provided they were
            // unique in chars1.
            for (var i = start2; i < end2; i++)
            {
                var c = chars2[i];
                if (unique1[c] < 0)
                {
                    continue;
                }

                unique2[c] = unique2[c] == -1 ? i : -2;
            }

            // Extract common unique subsequences.
            var common1 = new List<int>();
            var common2 = new List<int>();
            foreach (var i in subChars)
            {
                if (unique1[i] >= 0 && unique2[i] >= 0)
                {
                    common1.Add(unique1[i]);
                    common2.Add(unique2[i]);
                }

                unique1[i] = unique2[i] = -1;
            }

            // Repose the longest common subsequence as longest ascending
            // subsequence.  Note that common2 is already sorted by order of
            // appearance in file1 by char allocation.
            return common2.Count == 0
                ? []
                : LasIndices(common2).Select(x => (common1[x], common2[x]));
        }

        private static int[] LasIndices(List<int> sequence)
        {
            if (sequence.Count == 0)
            {
                return [];
            }

            var pileTops = new List<LcaNode> { new(0, null) };
            for (var i = 1; i < sequence.Count; i++)
            {
                var v = sequence[i];

                // Binary search for the first 'pileTop > v'.
                var a = 0;
                var b = pileTops.Count;
                while (a != b)
                {
                    var c = (a + b) / 2;
                    if (sequence[pileTops[c].Value] > v)
                    {
                        b = c;
                    }
                    else
                    {
                        a = c + 1;
                    }
                }

                if (a < pileTops.Count)
                {
                    pileTops[a] = new LcaNode(i, a > 0 ? pileTops[a - 1] : null);
                }
                else
                {
                    pileTops.Add(new LcaNode(i, pileTops[a - 1]));
                }
            }

            // Follow pointers back through path.
            var las = new int[pileTops.Count];
            var j   = pileTops.Count - 1;
            for (var node = pileTops[j]; node is not null; node = node.Previous)
            {
                las[j--] = node.Value;
            }

            return las;
        }
    }

    public static int[] Match(
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    )
    {
        return Match(new TokenMapper(), originalLines, modifiedLines);
    }

    internal static int[] Match(
        TokenMapper                      mapper,
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    )
    {
        var lineModeString1 = mapper.LinesToIds(originalLines);
        var lineModeString2 = mapper.LinesToIds(modifiedLines);

        return PatienceMatch.Match(lineModeString1, lineModeString2, mapper.MaxLineId);
    }
}

public static class LineMatchedDiffer
{
    public static int[] Match(
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    )
    {
        var mapper         = new TokenMapper();
        var matches        = PatienceDiffer.Match(mapper, originalLines, modifiedLines);
        var wordModeLines1 = originalLines.Select(mapper.WordsToIds).ToArray();
        var wordModeLines2 = modifiedLines.Select(mapper.WordsToIds).ToArray();

        // TODO: Figure out how to make configurable.
        new FuzzyLineMatcher().MatchLinesByWords(matches, wordModeLines1, wordModeLines2);
        return matches;
    }
}