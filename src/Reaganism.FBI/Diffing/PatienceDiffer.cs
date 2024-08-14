using System.Collections.Generic;
using System.Diagnostics;

using JetBrains.Annotations;

namespace Reaganism.FBI.Diffing;

[PublicAPI]
public class PatienceDiffer(TokenMapper? tokenMapper = null) : IDiffer
{
    private sealed class PatienceMatch
    {
        private class LcaNode(int value, LcaNode? previous)
        {
            public int Value { get; } = value;

            public LcaNode? Previous { get; } = previous;
        }

        private string? chars1;
        private string? chars2;
        private int[]?  unique1;
        private int[]?  unique2;
        private int[]?  matches;

        private readonly List<int> subChars = [];

        public int[] Match(string originalChars, string modifiedChars, int maxChar)
        {
            if (unique1 is null || unique1.Length < maxChar)
            {
                unique1 = new int[maxChar];
                unique2 = new int[maxChar];

                for (var i = 0; i < maxChar; i++)
                {
                    unique1[i] = unique2[i] = -1;
                }
            }

            chars1 = originalChars;
            chars2 = modifiedChars;

            return Match();
        }

        private int[] Match()
        {
            Debug.Assert(chars1 is not null);
            Debug.Assert(chars2 is not null);

            matches = new int[chars1.Length];
            for (var i = 0; i < chars1.Length; i++)
            {
                matches[i] = -1;
            }

            Match(0, chars1.Length, 0, chars2.Length);
            return matches;
        }

        private void Match(int start1, int end1, int start2, int end2)
        {
            Debug.Assert(chars1 is not null);
            Debug.Assert(chars2 is not null);
            Debug.Assert(matches is not null);

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
            foreach (var (m1, m2) in LcsUnique(start1, end1, start2, end2))
            {
                matches[m1] = m2;
                any         = true;

                // Step 4: Recurse.
                Match(start1, m1, start2, m2);

                start1 = m1 + 1;
                start2 = m2 + 1;
            }

            if (any)
            {
                // ReSharper disable once TailRecursiveCall
                Match(start1, end1, start2, end2);
            }
        }

        private IEnumerable<(int, int)> LcsUnique(int start1, int end1, int start2, int end2)
        {
            Debug.Assert(chars1 is not null);
            Debug.Assert(chars2 is not null);
            Debug.Assert(unique1 is not null);
            Debug.Assert(unique2 is not null);

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

                unique1[1] = unique2[i] = -1;
            }

            subChars.Clear();

            if (common2.Count == 0)
            {
                yield break;
            }

            // Repose the longest common subsequence as longest ascending
            // subsequence.  Note that common2 is already sorted by order of
            // appearance in file1 by char allocation.
            foreach (var i in LasIndices(common2))
            {
                yield return (common1[i], common2[i]);
            }
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

    [PublicAPI]
    public TokenMapper TokenMapper { [PublicAPI] get; } = tokenMapper ?? new TokenMapper();

    private string? lineModeString1;
    private string? lineModeString2;

    [PublicAPI]
    public virtual int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines)
    {
        lineModeString1 = TokenMapper.LinesToIds(originalLines);
        lineModeString2 = TokenMapper.LinesToIds(modifiedLines);

        return new PatienceMatch().Match(lineModeString1, lineModeString2, TokenMapper.MaxLineId);
    }
}