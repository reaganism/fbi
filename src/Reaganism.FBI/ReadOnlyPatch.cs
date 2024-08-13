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
        TrimmedRange1 = Patch.TrimRange(Range1, patch.Diffs);
        TrimmedRange2 = Patch.TrimRange(Range2, patch.Diffs);
    }
}