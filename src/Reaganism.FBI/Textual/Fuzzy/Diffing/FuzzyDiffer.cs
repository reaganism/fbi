using System.Collections.Generic;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Diffing;

/// <summary>
///     Static diffing operations for fuzzy diffing.
/// </summary>
public static class FuzzyDiffer
{
    public delegate int[] DiffMatch(
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    );
}