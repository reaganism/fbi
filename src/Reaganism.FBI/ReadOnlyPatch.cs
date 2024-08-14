using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

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
[PublicAPI]
public readonly struct ReadOnlyPatch
{
    /// <summary>
    ///     The diffs this patch collates.
    /// </summary>
    [PublicAPI]
    public IReadOnlyCollection<DiffLine> Diffs { [PublicAPI] get; }

    /// <summary>
    ///     Contextual lines which do not introduce new content.
    /// </summary>
    /// <remarks>
    ///     Contains both unmodified (EQUALS) and removed (DELETE) lines, as
    ///     they both provide context for the location of the patch in the
    ///     original text.
    /// </remarks>
    [PublicAPI]
    public IReadOnlyCollection<string> ContextLines { [PublicAPI] get; }

    /// <summary>
    ///     Lines which should be present in the modified text.
    /// </summary>
    /// <remarks>
    ///     This includes both unmodified (EQUALS) and added (INSERT) lines.
    /// </remarks>
    [PublicAPI]
    public IReadOnlyCollection<string> PatchedLines { [PublicAPI] get; }

    /// <summary>
    ///     The range of the first text.
    /// </summary>
    [PublicAPI]
    public LineRange Range1 { [PublicAPI] get; [PublicAPI] init; }

    /// <summary>
    ///     The range of the second text.
    /// </summary>
    [PublicAPI]
    public LineRange Range2 { [PublicAPI] get; [PublicAPI] init; }

    /// <summary>
    ///     The trimmed range of the first text, keeping only meaningful
    ///     information.
    /// </summary>
    [PublicAPI]
    public LineRange TrimmedRange1 { [PublicAPI] get; }

    /// <summary>
    ///     The trimmed range of the second text, keeping only meaningful
    ///     information.
    /// </summary>
    [PublicAPI]
    public LineRange TrimmedRange2 { [PublicAPI] get; }

    [PublicAPI]
    public ReadOnlyPatch(Patch patch)
    {
        // Ensure lengths are recalculated.
        patch.RecalculateLength();

        // Shallow-clone the diffs; we don't need to reinitialize since they're
        // structs.  TODO(perf): It'd be nice to avoid this operation.
        Diffs = patch.Diffs.ToList();

        ContextLines = Diffs.Where(diff => diff.Operation != Operation.INSERT)
                            .Select(x => x.Text)
                            .ToList();

        PatchedLines = Diffs.Where(diff => diff.Operation != Operation.DELETE)
                            .Select(x => x.Text)
                            .ToList();

        Range1        = new LineRange(patch.Start1, 0).WithLength(patch.Length1);
        Range2        = new LineRange(patch.Start2, 0).WithLength(patch.Length2);
        TrimmedRange1 = Patch.TrimRange(Range1, patch.Diffs);
        TrimmedRange2 = Patch.TrimRange(Range2, patch.Diffs);
    }

    [PublicAPI]
    public Patch CreateMutable()
    {
        return new Patch(Diffs.ToList())
        {
            Start1  = Range1.Start,
            Start2  = Range2.Start,
            Length1 = Range1.Length,
            Length2 = Range2.Length,
        };
    }
}