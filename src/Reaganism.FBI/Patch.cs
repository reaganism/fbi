using System.Collections.Generic;

namespace Reaganism.FBI;

/// <summary>
///     Represents a patch, which is a collection of diffs.
/// </summary>
public sealed class Patch
{
    /// <summary>
    ///     The diffs this patch collates.
    /// </summary>
    public List<Diff> Diffs { get; } = [];

    /// <summary>
    ///     The range of the first text.
    /// </summary>
    public LineRange Range1 { get; set; }

    /// <summary>
    ///     The range of the second text.
    /// </summary>
    public LineRange Range2 { get; set; }

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
    ///     Creates a <see cref="ReadOnlyPatch"/> derived from the current state
    ///     of this patch.
    /// </summary>
    /// <returns>The read-only patch with extra information.</returns>
    public ReadOnlyPatch AsReadOnly()
    {
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
}