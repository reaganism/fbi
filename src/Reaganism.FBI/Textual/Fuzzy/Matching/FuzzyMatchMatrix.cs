using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Matching;

// TODO: Heavily optimize after rewrite.
//       - Can MatchNode be a struct?
//       - Convert to stack allocations where possible.
//       - Attempt aggressive algorith optimizations; remove branching?

internal sealed class FuzzyMatchMatrix
{
    private sealed class MatchNode
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
        public MatchNode[] Nodes { get; }

        private readonly int                   patternLength;
        private readonly IReadOnlyList<string> pattern;
        private readonly IReadOnlyList<string> search;
        private readonly LineRange             range;

        public StraightMatch(
            IReadOnlyList<string> pattern,
            IReadOnlyList<string> search,
            LineRange             range
        )
        {
            patternLength = pattern.Count;
            this.pattern  = pattern;
            this.search   = search;
            this.range    = range;

            Nodes = new MatchNode[patternLength];
            for (var i = 0; i < patternLength; i++)
            {
                Nodes[i] = new MatchNode();
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
    ///     <see cref="maxOffset"/>.  The first entry is for pattern starting at
    ///     loc in text, the last entry is <c>offset</c> +
    ///     <see cref="maxOffset"/>.
    /// </summary>
    private readonly StraightMatch[] matches;

    /// <summary>
    ///     Location of first pattern line in search lines.  Starting offset
    ///     for a match.
    /// </summary>
    private int pos = int.MaxValue;

    private int firstNode;

    public FuzzyMatchMatrix(
        IReadOnlyList<string> pattern,
        IReadOnlyList<string> search,
        int                   maxOffset = DEFAULT_MAX_OFFSET,
        LineRange?            range     = null
    )
    {
        range ??= new LineRange().WithLength(search.Count);

        patternLength  = pattern.Count;
        this.range     = range.Value;
        this.maxOffset = maxOffset;
        WorkingRange   = new LineRange(this.range.Start - maxOffset, 0).WithLast(this.range.End - patternLength);

        matches = new StraightMatch[maxOffset + 1];
        for (var i = 0; i <= maxOffset; i++)
        {
            matches[i] = new StraightMatch(pattern, search, this.range);
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
    ///     Calculates the best path through the match matrix.  All paths
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
            node.Next = -1; // No next.
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
                if (sum <= maxSum)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LocInRange(int loc)
    {
        return range.Contains(loc) ? loc : -1;
    }

    /// <summary>
    ///     Returns the pattern distance between two successive nodes in a
    ///     path with offsets <paramref name="i"/> and <paramref name="j"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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