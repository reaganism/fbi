using System.Collections.Generic;

using JetBrains.Annotations;

using Reaganism.FBI.Textual.Fuzzy.Matching;
using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Diffing;

/// <summary>
///     A differ that can produce a diff between two collections of lines.
/// </summary>
public interface IDiffer
{
    /// <summary>
    ///     The mapper used to map tokens to their unique IDs.
    /// </summary>
    [PublicAPI]
    FuzzyTokenMapper Mapper { get; }

    /// <summary>
    ///     Matches the original lines and the modified lines.
    /// </summary>
    /// <param name="originalLines">The original file lines.</param>
    /// <param name="modifiedLines">The modified file lines.</param>
    /// <returns>
    ///     TODO
    /// </returns>
    [PublicAPI]
    int[] Match(
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    );
}

[PublicAPI]
public static class DifferExtensions
{
    [PublicAPI]
    public static List<FuzzyDiffLine> Diff(
        this IDiffer               @this,
        IReadOnlyList<Utf16String> originalLines,
        IReadOnlyList<Utf16String> modifiedLines
    )
    {
        return FuzzyLineMatching.MakeDiffList(@this.Match(originalLines, modifiedLines), originalLines, modifiedLines);
    }

    [PublicAPI]
    public static IEnumerable<FuzzyPatch> MakePatches(
        this IDiffer               @this,
        IReadOnlyList<Utf16String> originalLines,
        IReadOnlyList<Utf16String> modifiedLines,
        int                        contextLinesCount = FuzzyDiffer.DEFAULT_CONTEXT_COUNT,
        bool                       collate           = true
    )
    {
        return FuzzyDiffer.MakePatches(@this.Diff(originalLines, modifiedLines), contextLinesCount, collate);
    }
}