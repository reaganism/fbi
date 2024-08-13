using System;
using System.Collections.Generic;
using System.Linq;

using Reaganism.FBI.Extensions;

namespace Reaganism.FBI.Diffing;

public class LineMatchedDiffer(TokenMapper? tokenMapper = null) : PatienceDiffer(tokenMapper)
{
    private sealed class MatchMatrix
    {
        private sealed class MatchNodes
        {
            /// <summary>
            ///     The score of the match.
            /// </summary>
            /// <remarks>
            ///     <c>1.0</c> is a perfect match, <c>0.0</c> is no match.
            /// </remarks>
            public float Score { get; set; }

            /// <summary>
            ///     Sum of the match scores in the best path up to this node.
            /// </summary>
            public float Sum { get; set; }

            /// <summary>
            ///     Offset index of the next node in the path.
            /// </summary>
            public int Next { get; set; }
        }

        /// <summary>
        ///     Contains match entries for consecutive characters of a pattern
        ///     and the search text starting at line offset loc.
        /// </summary>
        private sealed class StraightMatch
        {
            public MatchNodes[] Nodes { get; }

            private readonly int                   patternLength;
            private readonly IReadOnlyList<string> pattern;
            private readonly IReadOnlyList<string> search;
            private readonly LineRange             range;

            public StraightMatch(IReadOnlyList<string> pattern, IReadOnlyList<string> search, LineRange range)
            {
                patternLength = pattern.Count;
                this.pattern  = pattern;
                this.search   = search;
                this.range    = range;

                Nodes = new MatchNodes[patternLength];
                for (var i = 0; i < patternLength; i++)
                {
                    Nodes[i] = new MatchNodes();
                }
            }

            public void Update(int loc)
            {
                for (var i = 0; i < patternLength; i++)
                {
                    var l = i + loc;
                    if (l < range.Start || l >= range.End)
                    {
                        Nodes[i].Score = 0f;
                    }
                    else
                    {
                        Nodes[i].Score = FuzzyLineMatcher.MatchLines(pattern[i], search[l]);
                    }
                }
            }
        }

        public const int DEFAULT_MAX_OFFSET = 5;

        public LineRange WorkingRange { get; }

        private readonly int       patternLength;
        private readonly LineRange range;

        /// <summary>
        ///     Maximum offset between line matches in a run.
        /// </summary>
        private readonly int maxOffset;

        /// <summary>
        ///     Consecutive matches for pattern offset from loc by up to
        ///     <see cref="maxOffset"/>. First entry is for pattern starting at
        ///     loc in text, last entry is <c>offset</c> +
        ///     <see cref="maxOffset"/>.
        /// </summary>
        private readonly StraightMatch[] matches;

        /// <summary>
        ///     Location of first pattern line in search lines. Starting offset
        ///     for a match.
        /// </summary>
        private int pos = int.MaxValue;

        private int firstNode;

        public MatchMatrix(
            IReadOnlyList<string> pattern,
            IReadOnlyList<string> search,
            int                   maxOffset = DEFAULT_MAX_OFFSET,
            LineRange             range     = default
        )
        {
            if (range == default(LineRange))
            {
                range = new LineRange().WithLength(search.Count);
            }

            patternLength  = pattern.Count;
            this.range     = range;
            this.maxOffset = maxOffset;
            WorkingRange   = new LineRange(range.Start - maxOffset, 0).WithLast(range.End - patternLength);

            matches = new StraightMatch[maxOffset + 1];
            for (var i = 0; i <= maxOffset; i++)
            {
                matches[i] = new StraightMatch(pattern, search, range);
            }
        }

        public bool Match(int loc, out float score)
        {
            score = 0f;

            if (!WorkingRange.Contains(loc))
            {
                return false;
            }

            if (loc == pos + 1)
            {
                StepForward();
            }
            else if (loc == pos - 1)
            {
                StepBackward();
            }
            else
            {
                Init(loc);
            }

            score = Recalculate();
            return true;
        }

        private void Init(int loc)
        {
            pos = loc;

            for (var i = 0; i <= maxOffset; i++)
            {
                matches[i].Update(loc + i);
            }
        }

        private void StepForward()
        {
            pos++;

            var reuse = matches[0];
            for (var i = 1; i <= maxOffset; i++)
            {
                matches[i - 1] = matches[i];
            }

            matches[maxOffset] = reuse;
            reuse.Update(pos + maxOffset);
        }

        private void StepBackward()
        {
            pos--;

            var reuse = matches[maxOffset];
            for (var i = maxOffset; i > 0; i--)
            {
                matches[i] = matches[i - 1];
            }

            matches[0] = reuse;
            reuse.Update(pos);
        }

        /// <summary>
        ///     Calculates the best path through the match matrix. All paths
        ///     must start with the first line of pattern matched to the line at
        ///     loc (0 offset).
        /// </summary>
        private float Recalculate()
        {
            // Tail nodes have `sum = score`.
            for (var j = 0; j <= maxOffset; j++)
            {
                var node = matches[j].Nodes[patternLength - 1];
                node.Sum  = node.Score;
                node.Next = -1;
            }

            // Calculate the best paths for all nodes excluding the head.
            for (var i = patternLength - 2; i >= 0; i--)
            for (var j = 0; j <= maxOffset; j++)
            {
                var node   = matches[j].Nodes[i];
                var maxK   = -1;
                var maxSum = 0f;

                for (var k = 0; k <= maxOffset; k++)
                {
                    var l = i + OffsetsToPatternDistance(j, k);
                    if (l >= patternLength)
                    {
                        continue;
                    }

                    var sum = matches[k].Nodes[l].Sum;
                    if (k > j)
                    {
                        // Penalty for skipping lines in search text.
                        sum -= 0.5f * (k - j);
                    }

                    if (sum > maxSum)
                    {
                        maxK   = k;
                        maxSum = sum;
                    }
                }

                node.Sum  = maxSum + node.Score;
                node.Next = maxK;
            }

            // Find starting node.
            {
                firstNode = 0;
                var maxSum = matches[0].Nodes[0].Sum;
                for (var k = 1; k <= maxOffset; k++)
                {
                    var sum = matches[k].Nodes[0].Sum;
                    if (!(sum > maxSum))
                    {
                        continue;
                    }

                    firstNode = k;
                    maxSum    = sum;
                }
            }

            // Return best path value.
            return matches[firstNode].Nodes[0].Sum / patternLength;
        }

        /// <summary>
        ///     Get the path of the current best match.
        /// </summary>
        /// <returns>
        ///     An array of corresponding line numbers in search text for each
        ///     line in the pattern.
        /// </returns>
        public int[] Path()
        {
            var path = new int[patternLength];

            // Offset of current node.
            var offset = firstNode;
            var node   = matches[firstNode].Nodes[0];
            path[0] = LocInRange(pos + offset);

            // Index in the pattern of current node.
            var i = 0;
            while (node.Next >= 0)
            {
                var delta = OffsetsToPatternDistance(offset, node.Next);
                while (delta-- > 1)
                {
                    // Skipped pattern lines.
                    path[++i] = -1;
                }

                offset  = node.Next;
                node    = matches[offset].Nodes[++i];
                path[i] = LocInRange(pos + i + offset);
            }

            // Trailing lines with no match.
            while (++i < path.Length)
            {
                path[i] = -1;
            }

            return path;
        }

        private int LocInRange(int loc)
        {
            return range.Contains(loc) ? loc : -1;
        }

        /*public string Visualize()
        {
            var path = Path();
            var sb   = new StringBuilder();
            for (var j = 0; j <= maxOffset; j++)
            {
                sb.Append(j).Append(':');
                var line = matches[j];
                for (var i = 0; i < patternLength; i++)
                {
                    var inPath = path[i] > 0 && path[i] == pos + i + j;
                    sb.Append(inPath ? '[' : ' ');
                    var score = (int)Math.Round(line.Nodes[i].Score * 100);
                    sb.Append(score == 100 ? "%%" : score.ToString("D2"));
                    sb.Append(inPath ? ']' : ' ');
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }*/

        /// <summary>
        ///     Returns the pattern distance between two successive nodes in a
        ///     path with offsets <paramref name="i"/> and <paramref name="j"/>.
        /// </summary>
        private static int OffsetsToPatternDistance(int i, int j)
        {
            // i == j: line offsets are the same and the distance is 1 line.
            // j > i: offset increased by j - i in successive pattern lines and
            //        the distance is 1 line.
            // j < i: i - j patch lines were skipped between nodes and the
            //        distance is 1 + i - j.
            return j >= i ? 1 : 1 + i - j;
        }
    }

    private sealed class FuzzyLineMatcher
    {
        private const float default_min_match_score = 0.5f;

        public int MaxMatchOffset { get; set; } = MatchMatrix.DEFAULT_MAX_OFFSET;

        public float MinMatchScore { get; set; } = default_min_match_score;

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

        public static int LevenshteinDistance(string s, string t)
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

    private string[]? WordModeLines1 { get; set; }

    private string[]? WordModeLines2 { get; set; }

    public int MaxMatchOffset { get; set; }

    public float MinMatchScore { get; set; }

    public int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines)
    {
        var matches = base.Match(originalLines, modifiedLines);
        WordModeLines1 = originalLines.Select(TokenMapper.WordsToIds).ToArray();
        WordModeLines2 = modifiedLines.Select(TokenMapper.WordsToIds).ToArray();

        new FuzzyLineMatcher
        {
            MinMatchScore  = MinMatchScore,
            MaxMatchOffset = MaxMatchOffset,
        }.MatchLinesByWords(matches, WordModeLines1, WordModeLines2);

        return matches;
    }
}