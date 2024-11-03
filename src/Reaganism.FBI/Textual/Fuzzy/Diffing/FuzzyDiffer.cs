using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Diffing;

/// <summary>
///     Static diffing operations for fuzzy diffing.
/// </summary>
public static class FuzzyDiffer
{
    [PublicAPI]
    public const int DEFAULT_CONTEXT_COUNT = 3;

    /// <summary>
    ///     Produces a patch file from two files (an original file and a
    ///     modified file).
    /// </summary>
    /// <param name="differ">The differ to use for diffing.</param>
    /// <param name="originalLines">The original text lines.</param>
    /// <param name="modifiedLines">The modified text lines.</param>
    /// <param name="contextLinesCount">
    ///     The amount of surrounding context.
    /// </param>
    /// <param name="collate">Whether patches should be collated.</param>
    /// <returns>The generated patches.</returns>
    [PublicAPI]
    public static IEnumerable<FuzzyPatch> DiffTexts(
        IDiffer                    differ,
        IReadOnlyList<Utf16String> originalLines,
        IReadOnlyList<Utf16String> modifiedLines,
        int                        contextLinesCount = DEFAULT_CONTEXT_COUNT,
        bool                       collate           = true
    )
    {
        return differ.MakePatches(
            originalLines,
            modifiedLines,
            contextLinesCount,
            collate
        ).ToList();
    }

    /// <summary>
    ///     Converts a collection of diffs into a collection patches.
    /// </summary>
    /// <param name="diffs">The diffs to convert.</param>
    /// <param name="contextLinesCount">
    ///     The amount of surrounding context.
    /// </param>
    /// <param name="collate">Whether patches should be collated.</param>
    /// <returns>The produced patch collection.</returns>
    internal static IEnumerable<FuzzyPatch> MakePatches(
        List<FuzzyDiffLine> diffs,
        int                 contextLinesCount = DEFAULT_CONTEXT_COUNT,
        bool                collate           = true
    )
    {
        var patch = new FuzzyPatch(diffs);
        patch.RecalculateLengths();
        patch.Trim(contextLinesCount);

        if (patch.Length1 == 0)
        {
            return [];
        }

        if (!collate)
        {
            patch = patch.Uncollate();
        }

        return patch.Split(contextLinesCount);
    }
}