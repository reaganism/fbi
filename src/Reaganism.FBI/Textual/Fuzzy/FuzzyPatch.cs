using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Utility;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     A patch hunk, which is a collection of <see cref="FuzzyDiffLine"/>s.
/// </summary>
/// <remarks>
///     This is a mutable representation that requires manual recalculations
///     under rare cases (in which <see cref="RecalculateLengths"/> should be
///     called).  TODO: When?
/// </remarks>
[PublicAPI]
public sealed class FuzzyPatch
{
    /// <summary>
    ///     The diffs that make up this patch.
    /// </summary>
    [PublicAPI]
    public List<FuzzyDiffLine> Diffs { get; set; }

#region Ranges
    /// <summary>
    ///     The starting line number of the original file hunk.
    /// </summary>
    internal int Start1 { get; set; }

    /// <summary>
    ///     The starting line number of the modified file hunk.
    /// </summary>
    internal int Start2 { get; set; }

    /// <summary>
    ///     The length of the original file hunk.
    /// </summary>
    internal int Length1 { get; set; }

    /// <summary>
    ///     The length of the modified file hunk.
    /// </summary>
    internal int Length2 { get; set; }
#endregion

#region Computed data - previously: CompiledPatch
    /// <summary>
    ///     The context lines (all lines but INSERT operations) of this patch.
    /// </summary>
    /// <remarks>
    ///     Cache the result! This is expensive and forces string allocations.
    /// </remarks>
    [PublicAPI]
    public IReadOnlyCollection<string> ContextLines =>
        Diffs.Where(x => x.Operation != FuzzyOperation.INSERT)
             .Select(x => x.ToString())
             .ToList();

    /// <summary>
    ///     The patched lines (all lines but DELETE operations) of this patch.
    /// </summary>
    /// <remarks>
    ///     Cache the result! This is expensive and forces string allocations.
    /// </remarks>
    [PublicAPI]
    public IReadOnlyCollection<string> PatchedLines =>
        Diffs.Where(x => x.Operation != FuzzyOperation.DELETE)
             .Select(x => x.ToString())
             .ToList();

    /// <summary>
    ///     The range of the original file hunk.
    /// </summary>
    [PublicAPI]
    public LineRange Range1 => new LineRange(Start1, 0).WithLength(Length1);

    /// <summary>
    ///     The range of the modified file hunk.
    /// </summary>
    [PublicAPI]
    public LineRange Range2 => new LineRange(Start2, 0).WithLength(Length2);

    /// <summary>
    ///     The trimmed range of the original file hunk.
    /// </summary>
    [PublicAPI]
    public LineRange TrimmedRange1 => TrimRange(Range1, Diffs);

    /// <summary>
    ///     The trimmed range of the modified file hunk.
    /// </summary>
    [PublicAPI]
    public LineRange TrimmedRange2 => TrimRange(Range2, Diffs);
#endregion

    /// <summary>
    ///     Creates a new <see cref="FuzzyPatch"/> with the given
    ///     <paramref name="diffs"/>.
    /// </summary>
    /// <param name="diffs">The diffs of this patch.</param>
    [PublicAPI]
    public FuzzyPatch(List<FuzzyDiffLine>? diffs = null)
    {
        Diffs = diffs ?? [];
    }

    /// <summary>
    ///     Recalculates the lengths of the original and modified file hunks.
    /// </summary>
    /// <remarks>
    ///     Important to call if <see cref="Diffs"/> is ever modified.
    /// </remarks>
    internal void RecalculateLengths()
    {
        Length1 = Diffs.Count;
        Length2 = Diffs.Count;

        foreach (var diff in Diffs)
        {
            if (diff.Operation == FuzzyOperation.INSERT)
            {
                Length1--;
            }
            else if (diff.Operation == FuzzyOperation.DELETE)
            {
                Length2--;
            }
        }
    }

    /// <summary>
    ///     Trims the ranges of this patch to the given
    ///     <paramref name="contextLineCount"/>.  Specifically, it removes any
    ///     contiguous context lines exceeding the given count.
    /// </summary>
    /// <param name="contextLineCount">The number of context lines.</param>
    [PublicAPI]
    public void Trim(int contextLineCount)
    {
        var range = TrimRange(new LineRange(0, 0).WithLength(Diffs.Count), Diffs);

        if (range.Length == 0)
        {
            Length1 = 0;
            Length2 = 0;
            Diffs.Clear();
        }

        var trimStart = range.Start - contextLineCount;
        var trimEnd   = Diffs.Count - range.End - contextLineCount;
        {
            if (trimStart > 0)
            {
                Diffs.RemoveRange(0, trimStart);
                Start1  += trimStart;
                Start2  += trimStart;
                Length1 -= trimStart;
                Length2 -= trimStart;
            }

            if (trimEnd > 0)
            {
                Diffs.RemoveRange(Diffs.Count - trimEnd, trimEnd);
                Length1 -= trimEnd;
                Length2 -= trimEnd;
            }
        }
    }

    /// <summary>
    ///     "Uncollates" the patch, cleaning up ordering so chunks of deletions
    ///     and insertions are separated and positioned properly (grouped
    ///     together).
    /// </summary>
    /// <remarks>
    ///     This cleans up scenarios in which, say, four lines are deleted and
    ///     replaced with another four lines, but the patch is represented as
    ///     <c>-+-+-+-+</c> when it instead could be <c>----++++</c>.
    ///     <br />
    ///     Our implementations should not result in this (the latter should
    ///     always be produced), but this may serve useful when working with
    ///     externally-produced patches or custom algorithms.
    /// </remarks>
    /// <returns>
    ///     A new <see cref="FuzzyPatch"/> with the sorted diff contents.
    /// </returns>
    [PublicAPI]
    public FuzzyPatch Uncollate()
    {
        // The uncollated list is our processed list of diffs.  This method does
        // not remove any diffs, it only does re-ordering to tidy up the patch.
        // The insertions list is a temporary list of any insertions we find
        // that will be held onto until we find a deletion to pair them with.
        var uncollated = new List<FuzzyDiffLine>(Diffs.Count);
        var insertions = new List<FuzzyDiffLine>();

        foreach (var diff in Diffs)
        {
            if (diff.Operation == FuzzyOperation.DELETE)
            {
                // If this is a DELETE, we immediately move it to the copied
                // collection.  We are ordering DELETE operations before INSERT
                // operations.
                uncollated.Add(diff);
            }
            else if (diff.Operation == FuzzyOperation.INSERT)
            {
                // If this is an INSERT, hold onto it until we find the end of
                // the chunk.
                insertions.Add(diff);
            }
            else
            {
                // Currently, the only other operation is EQUALS.
                Debug.Assert(diff.Operation == FuzzyOperation.EQUALS, "Unexpected operation");

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
        return new FuzzyPatch(uncollated);
    }

    /// <summary>
    ///     Splits the current patch into multiple, smaller patches.  When there
    ///     are fewer context lines, removing extras may result in a single
    ///     patch fragmenting into multiple.  This is because one large patch
    ///     may have two diff chunks with context lines that touch or merge with
    ///     each other, creating one large patch, and reducing the amount of
    ///     context results in these chunks no longer being connected (thus
    ///     requiring multiple patches).
    /// </summary>
    /// <param name="contextLineCount">The amount of context lines.</param>
    /// <returns>
    ///     A collection of patches that are the result of splitting the current
    ///     patch.
    /// </returns>
    [PublicAPI]
    public IEnumerable<FuzzyPatch> Split(int contextLineCount)
    {
        // We can short-circuit if there are no diffs.
        if (Diffs.Count == 0)
        {
            return [];
        }

        var ranges = new List<LineRange>();

        var start        = 0;
        var contextCount = 0;
        for (var i = 0; i < Diffs.Count; i++)
        {
            var diff = Diffs[i];

            // EQUALS operations are context.
            if (diff.Operation == FuzzyOperation.EQUALS)
            {
                contextCount++;
                continue;
            }

            // Two patches can border each other, and the farthest apart they
            // can be is double the context line count (maximum context from
            // either patch).  If we have encountered an amount of contiguous
            // context greater than double the count, we know they need to be
            // split.
            if (contextCount > contextLineCount * 2)
            {
                ranges.Add(new LineRange(start, i - contextCount + contextLineCount));
                start = i - contextLineCount;
            }

            contextCount = 0;
        }

        // Add the final range.
        ranges.Add(new LineRange(start, Diffs.Count));

        var patches  = new List<FuzzyPatch>(ranges.Count);
        var end1     = Start1;
        var end2     = Start2;
        var endIndex = 0;
        foreach (var range in ranges)
        {
            var skip = range.Start - endIndex;
            var patch = new FuzzyPatch(Diffs.Slice(range).ToList())
            {
                Start1 = end1 + skip,
                Start2 = end2 + skip,
            };
            patch.RecalculateLengths();
            patches.Add(patch);

            end1     = patch.Start1 + patch.Length1;
            end2     = patch.Start2 + patch.Length2;
            endIndex = range.End;
        }

        return patches;
    }

    /// <summary>
    ///     Produces a new <see cref="LineRange"/> with any unnecessary context
    ///     removed.
    /// </summary>
    /// <param name="range">The range to trim.</param>
    /// <param name="diffs">
    ///     The <see cref="FuzzyDiffLine"/>s the <paramref name="range"/> is
    ///     describing.
    /// </param>
    /// <returns>
    ///     The trimmed <see cref="LineRange"/>.
    /// </returns>
    private static LineRange TrimRange(
        LineRange           range,
        List<FuzzyDiffLine> diffs
    )
    {
        var start = 0;

        // Find the first non-EQUALS (meaningful) diff.
        while (start < diffs.Count && diffs[start].Operation == FuzzyOperation.EQUALS)
        {
            start++;
        }

        // If we've already reached the end then there's no meaningful content.
        if (start == diffs.Count)
        {
            // We need to return something, so the start of the range may remain
            // the same, but we give it a length of 0 to represent it's
            // functionally a no-op.
            return range.WithLength(0);
        }

        // Now that we've located the start, we need to determine the end.
        var end = diffs.Count;
        while (end > start && diffs[end - 1].Operation == FuzzyOperation.EQUALS)
        {
            end--;
        }

        // Our new line range includes the offsets we've calculated to remove
        // any unnecessary context.
        return new LineRange(range.Start + start, range.End - (diffs.Count - end));
    }
}