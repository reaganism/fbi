using System.Collections.Generic;

using JetBrains.Annotations;

using Reaganism.FBI.Diffing;
using Reaganism.FBI.Matching;

namespace Reaganism.FBI.Utilities.Extensions;

[PublicAPI]
public static class DifferExtensions
{
    [PublicAPI]
    public static List<DiffLine> Diff(
        this IDiffer          @this,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines
    )
    {
        return LineMatching.MakeDiffList(@this.Match(originalLines, modifiedLines), originalLines, modifiedLines);
    }

    [PublicAPI]
    public static IEnumerable<ReadOnlyPatch> MakePatches(
        this IDiffer          @this,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines,
        int                   contextLinesCount = Differ.DEFAULT_CONTEXT_COUNT,
        bool                  collate           = true
    )
    {
        return Differ.MakePatches(@this.Diff(originalLines, modifiedLines), contextLinesCount, collate);
    }
}