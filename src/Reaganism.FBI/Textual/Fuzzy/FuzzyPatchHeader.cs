using System.Collections.Generic;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     Provides a way to get the header for a fuzzy patch.
/// </summary>
public static class FuzzyPatchHeader
{
    private static readonly Dictionary<(LineRange, LineRange), string> auto_headers = [];
    private static readonly Dictionary<(LineRange, LineRange), string> headers      = [];

    /// <summary>
    ///     Gets the (cached) header for the given patch.
    /// </summary>
    /// <param name="patch">The patch to get the header for.</param>
    /// <param name="auto">
    ///     Whether the insertion offset should be determined automatically.
    /// </param>
    /// <returns>The header.</returns>
    public static string GetHeader(FuzzyPatch patch, bool auto)
    {
        return GetHeader(patch.Range1, patch.Range2, auto);
    }

    /// <summary>
    ///     Gets the (cached) header for the given patch.
    /// </summary>
    /// <param name="range1">The first (DELETE) range.</param>
    /// <param name="range2">The second (INSERT) range.</param>
    /// <param name="auto">
    ///     Whether the insertion offset should be determined automatically.
    /// </param>
    /// <returns>The header.</returns>
    public static string GetHeader(LineRange range1, LineRange range2, bool auto)
    {
        var map = auto ? auto_headers : headers;

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