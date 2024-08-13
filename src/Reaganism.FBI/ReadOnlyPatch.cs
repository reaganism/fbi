using System.Collections.Generic;
using System.Linq;

namespace Reaganism.FBI;

/// <summary>
///     A read-only patch derived from a mutable <see cref="Patch"/> instance,
///     containing contextualized data generated from the final state of the
///     <see cref="Patch"/> instance it was created from.
/// </summary>
/// <remarks>
///     <see cref="Patch"/> should be used for the initial creation and basic
///     processing. <see cref="ReadOnlyPatch"/>es are for working with finalized
///     data.
/// </remarks>
/// <seealso cref="Patch"/>
public readonly struct ReadOnlyPatch
{
    /// <summary>
    ///     The diffs this patch collates.
    /// </summary>
    public IReadOnlyCollection<Diff> Diffs { get; }

    /// <summary>
    ///     Contextual lines which do not introduce new content.
    /// </summary>
    /// <remarks>
    ///     Contains both unmodified (EQUALS) and removed (DELETE) lines, as
    ///     they both provide context for the location of the patch in the
    ///     original text.
    /// </remarks>
    public IReadOnlyCollection<string> ContextLines { get; }

    /// <summary>
    ///     Lines which should be present in the modified text.
    /// </summary>
    /// <remarks>
    ///     This includes both unmodified (EQUALS) and added (INSERT) lines.
    /// </remarks>
    public IReadOnlyCollection<string> PatchedLines { get; }

    /// <summary>
    ///     The range of the first text.
    /// </summary>
    public LineRange Range1 { get; }

    /// <summary>
    ///     The range of the second text.
    /// </summary>
    public LineRange Range2 { get; }

    /// <summary>
    ///     The trimmed range of the first text, keeping only meaningful
    ///     information.
    /// </summary>
    public LineRange TrimmedRange1 { get; }

    /// <summary>
    ///     The trimmed range of the second text, keeping only meaningful
    ///     information.
    /// </summary>
    public LineRange TrimmedRange2 { get; }

    public ReadOnlyPatch(Patch patch)
    {
        // Shallow-clone the diffs; we don't need to reinitialize since they're
        // structs.  TODO(perf): It'd be nice to avoid this operation.
        Diffs = [..patch.Diffs];

        ContextLines = Diffs.Where(diff => diff.Operation != Operation.INSERT)
                            .Select(diff => diff.Text)
                            .ToList();

        PatchedLines = Diffs.Where(diff => diff.Operation != Operation.DELETE)
                            .Select(diff => diff.Text)
                            .ToList();

        Range1        = patch.Range1;
        Range2        = patch.Range2;
        TrimmedRange1 = TrimRange(Range1, patch.Diffs);
        TrimmedRange2 = TrimRange(Range2, patch.Diffs);
    }

    /// <summary>
    ///     Produces a new line range with any unnecessary context removed.
    /// </summary>
    /// <param name="range">The range to trim.</param>
    /// <param name="diffs">The diffs this line range encompasses.</param>
    /// <returns>The trimmed line range.</returns>
    private static LineRange TrimRange(LineRange range, List<Diff> diffs)
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