using System.Collections.Generic;

using JetBrains.Annotations;

namespace Reaganism.FBI;

partial class Patch
{
    private static readonly Dictionary<(LineRange, LineRange), string> auto_headers = [];
    private static readonly Dictionary<(LineRange, LineRange), string> headers      = [];

    /// <summary>
    ///     Gets a cached header for the given patch.
    /// </summary>
    /// <param name="patch">The patch.</param>
    /// <param name="auto">
    ///     Whether insertion offsets should be automatically detected (ergo not
    ///     specified).
    /// </param>
    /// <returns>The header.</returns>
    [PublicAPI]
    public static string GetHeader(Patch patch, bool auto)
    {
        return GetHeader(
            new LineRange(patch.Start1, 0).WithLength(patch.Length1),
            new LineRange(patch.Start2, 0).WithLength(patch.Length2),
            auto
        );
    }

    /// <summary>
    ///     Gets a cached header for the given patch.
    /// </summary>
    /// <param name="patch">The patch.</param>
    /// <param name="auto">
    ///     Whether insertion offsets should be automatically detected (ergo not
    ///     specified).
    /// </param>
    /// <returns>The header.</returns>
    [PublicAPI]
    public static string GetHeader(CompiledPatch patch, bool auto)
    {
        return GetHeader(patch.Range1, patch.Range2, auto);
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
    [PublicAPI]
    public static string GetHeader(LineRange range1, LineRange range2, bool auto)
    {
        var map = auto ? auto_headers : headers;
        // var hash = range1.GetHashCode() ^ range2.GetHashCode();

        lock (map)
        {
            if (map.TryGetValue((range1, range2), out var header))
            {
                return header;
            }

            if (auto)
            {
                return map[(range1, range2)] = $"@@ -{range1.Start + 1},{range1.Length} +_,{range2.Length} @@";
            }

            return map[(range1, range2)] = $"@@ -{range1.Start + 1},{range1.Length} +{range2.Start + 1},{range2.Length} @@";
        }
    }
}