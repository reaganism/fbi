using System.Collections.Generic;
using System.Diagnostics;

namespace Reaganism.FBI;

/// <summary>
///     Represents a patch, which is a collection of diffs.
/// </summary>
/// <remarks>
///     This is a mutable representation designed for modifications before the
///     finalization of data. Most APIs are intended to consume
///     <see cref="ReadOnlyPatch"/>es, which are produced from data within this
///     class.
/// </remarks>
/// <seealso cref="ReadOnlyPatch"/>
public sealed class Patch
{
    /// <summary>
    ///     The diffs this patch collates.
    /// </summary>
    public List<DiffLine> Diffs { get; private set; } = [];

    /// <summary>
    ///     The range of the first text.
    /// </summary>
    /// <remarks>
    ///     The length of this range is not guaranteed to be accurate unless
    ///     <see cref="RecalculateRanges"/> is called.
    /// </remarks>
    internal LineRange Range1 { get; set; }

    /// <summary>
    ///     The range of the second text.
    /// </summary>
    /// <remarks>
    ///     The length of this range is not guaranteed to be accurate unless
    ///     <see cref="RecalculateRanges"/> is called.
    /// </remarks>
    internal LineRange Range2 { get; set; }

    private static readonly Dictionary<int, string> auto_headers = [];
    private static readonly Dictionary<int, string> headers      = [];

    public Patch() { }

    public Patch(Patch other)
    {
        // Shallow-clone the diffs; we don't need to reinitialize since they're
        // structs.
        Diffs = [..other.Diffs];

        Range1 = other.Range1;
        Range2 = other.Range2;
    }

    /// <summary>
    ///     Trims the ranges of this patch to the given context line count.
    /// </summary>
    /// <param name="contextLineCount">The amount of context lines.</param>
    public void Trim(int contextLineCount)
    {
        var range = TrimRange(new LineRange(0, Diffs.Count), Diffs);

        if (range.Length == 0)
        {
            Range1 = Range1 with { Length = 0 };
            Range2 = Range2 with { Length = 0 };
            Diffs.Clear();
            return;
        }

        var trimStart = range.Start - contextLineCount;
        var trimEnd   = Diffs.Count - range.End - contextLineCount;
        {
            if (trimStart > 0)
            {
                Diffs.RemoveRange(0, trimStart);
                Range1 = new LineRange(Start: Range1.Start + trimStart, Length: Range1.Length - trimStart);
                Range2 = new LineRange(Start: Range2.Start + trimStart, Length: Range2.Length - trimStart);
            }

            if (trimEnd > 0)
            {
                Diffs.RemoveRange(Diffs.Count                 - trimEnd, trimEnd);
                Range1 = Range1 with { Length = Range1.Length - trimEnd };
                Range2 = Range2 with { Length = Range2.Length - trimEnd };
            }
        }
    }

    /// <summary>
    ///     "Uncollates" the patch, cleaning up ordering so chunks of deletions
    ///     and insertions are separated and positioned properly.
    /// </summary>
    /// <remarks>
    ///     This cleans up scenarios in which, say, four lines and deleted and
    ///     replaced with another four lines, but the patch is represented as
    ///     <c>-+-+-+-+</c> when it could be <c>----++++</c>.
    /// </remarks>
    public void Uncollate()
    {
        // The uncollated list is our processed list of diffs.  This method does
        // not remove any diffs, it only does re-ordering to tidy up the patch.
        // The insertions list is a temporary list of any insertions we find
        // that can be held until we find a deletion to pair them with.
        var uncollated = new List<DiffLine>(Diffs.Count);
        var insertions = new List<DiffLine>();

        foreach (var diff in Diffs)
        {
            if (diff.Operation == Operation.DELETE)
            {
                // If this is a DELETE, we immediately move it to the copied
                // collection.  We are ordering DELETE operations before INSERT
                // operations.
                uncollated.Add(diff);
            }
            else if (diff.Operation == Operation.INSERT)
            {
                // If this is an INSERT, hold onto it until we find the end of
                // the chunk.
                insertions.Add(diff);
            }
            else
            {
                // Currently, the only other operation is EQUALS.
                Debug.Assert(diff.Operation == Operation.EQUALS);

                // If this is neither a DELETE nor an INSERT operation, we have
                // reached the end of a chunk of deletions and insertions.  We
                // need to uncollate them.
                {
                    // We can now add the insertions we've found and clear the
                    // list.
                    uncollated.AddRange(insertions);
                    insertions.Clear();
                }

                // Add the EQUALS diff since we're still preserving all
                // information.
                uncollated.Add(diff);
            }
        }

        // Final clean-up; add any remaining insertions (it's possible that a
        // patch does not end with any context thus the chunk does not "end").
        uncollated.AddRange(insertions);
        Diffs = uncollated;
    }

    /// <summary>
    ///     Splits the current patch into multiple, smaller patches. When there
    ///     are fewer context lines, removing extras may result in a single
    ///     patch fragmenting into multiple. This is because one large patch may
    ///     have two diff chunks with context lines that merge into each other,
    ///     creating one large patch, and reducing the amount of context results
    ///     in these chunks no longer being connected (thus requiring multiple
    ///     patches).
    /// </summary>
    /// <param name="contextLineCount">The amount of context lines.</param>
    /// <returns>
    ///     A collection of patches that are the result of splitting the current
    ///     patch.
    /// </returns>
    public IEnumerable<Patch> Split(int contextLineCount)
    {
        // We can short-circuit if there are no diffs.
        if (Diffs.Count == 0)
        {
            return [];
        }

        // Two patches can border each other, and the farthest apart they can be
        // is double the context line count (maximum context from either patch).
        var contiguousContextualLineCountMax = contextLineCount * 2;

        var ranges = new List<LineRange>();

        var start        = 0;
        var contextCount = 0;
        for (var i = 0; i < Diffs.Count; i++)
        {
            var diff = Diffs[i];

            // EQUALS operations are context.
            if (diff.Operation == Operation.EQUALS)
            {
                contextCount++;
                continue;
            }

            // If we have encountered an amount of contiguous context greater
            // than double the count, we know they need to be split.
            if (contextCount > contiguousContextualLineCountMax)
            {
                ranges.Add(new LineRange(start, i - contextCount + contextLineCount));
                start = i - contextLineCount;
            }

            contextCount = 0;
        }

        // Add the final range.
        ranges.Add(new LineRange(start, Diffs.Count));

        var patches      = new List<Patch>(ranges.Count);
        var end1         = Range1.Start;
        var end2         = Range2.Start;
        var endDiffIndex = 0;

        foreach (var range in ranges)
        {
            var skip = range.Start - endDiffIndex;
            var patch = new Patch
            {
                Range1 = new LineRange(end1 + skip, 0),
                Range2 = new LineRange(end2 + skip, 0),
                Diffs  = Diffs[range.Start..range.Length],
            };
            patch.RecalculateRanges();
            patches.Add(patch);

            end1         = patch.Range1.Start + patch.Range1.Length;
            end2         = patch.Range2.Start + patch.Range2.Length;
            endDiffIndex = range.End;
        }

        return patches;
    }

    /// <summary>
    ///     Creates a <see cref="ReadOnlyPatch"/> derived from the current state
    ///     of this patch.
    /// </summary>
    /// <returns>The read-only patch with extra information.</returns>
    public ReadOnlyPatch AsReadOnly()
    {
        RecalculateRanges();
        return new ReadOnlyPatch(this);
    }

    /// <summary>
    ///     Clones this patch.
    /// </summary>
    /// <returns>
    ///     A new <see cref="Patch"/> instance with the same data but without
    ///     any references to the original.
    /// </returns>
    public Patch Clone()
    {
        return new Patch(this);
    }

    private void RecalculateRanges()
    {
        var length1 = Diffs.Count;
        var length2 = Diffs.Count;

        foreach (var diff in Diffs)
        {
            if (diff.Operation == Operation.INSERT)
            {
                length1--;
            }
            else if (diff.Operation == Operation.DELETE)
            {
                length2--;
            }
        }

        Range1 = Range1 with { Length = length1 };
        Range2 = Range2 with { Length = length2 };
    }

    /// <summary>
    ///     Gets a cached header for the given ranges.
    /// </summary>
    /// <param name="range1">The first (DELETE) range.</param>
    /// <param name="range2">The second (INSERT) range.</param>
    /// <param name="auto">
    ///     Whether insertion offsets should be automatically detected (ergo not
    ///     specified).
    /// </param>
    /// <returns>The header.</returns>
    public static string GetHeader(LineRange range1, LineRange range2, bool auto)
    {
        var map  = auto ? auto_headers : headers;
        var hash = range1.GetHashCode() ^ range2.GetHashCode();

        if (map.TryGetValue(hash, out var header))
        {
            return header;
        }

        if (auto)
        {
            return map[hash] = $"@@ -{range1.Start + 1},{range1.Length} +_,{range2.Length} @@";
        }

        return map[hash] = $"@@ -{range1.Start + 1},{range1.Length} +{range2.Start + 1},{range2.Length} @@";
    }

    /// <summary>
    ///     Produces a new line range with any unnecessary context removed.
    /// </summary>
    /// <param name="range">The range to trim.</param>
    /// <param name="diffs">The diffs this line range encompasses.</param>
    /// <returns>The trimmed line range.</returns>
    internal static LineRange TrimRange(LineRange range, List<DiffLine> diffs)
    {
        // Fine the first non-EQUALS (meaningful) diff.
        var start = 0;
        while (start < diffs.Count && diffs[start].Operation == Operation.EQUALS)
        {
            start++;
        }

        // If we've reached the end already, there's no meaningful content.
        if (start == diffs.Count)
        {
            // We need to return something, so the start of the range may remain
            // the same, but we give it a length of 0 to represent it's
            // functionally a no-op.
            return range with { Length = 0 };
        }

        // Now that we've located the start, we need to determine the end.
        var end = 0;
        while (end > start && diffs[end - 1].Operation == Operation.EQUALS)
        {
            end--;
        }

        // Our new line range includes the offsets we've calculated to remove
        // any unnecessary context.
        return new LineRange(range.Start + start, range.Length - (diffs.Count - end));
    }
}