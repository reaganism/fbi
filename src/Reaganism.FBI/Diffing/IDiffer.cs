using System.Collections.Generic;

namespace Reaganism.FBI.Diffing;

public interface IDiffer
{
    TokenMapper TokenMapper { get; }

    int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines);
}

public static class DifferExtensions
{
    public static List<DiffLine> Diff(
        this IDiffer          @this,
        IReadOnlyList<string> originalLines,
        IReadOnlyList<string> modifiedLines
    )
    {
        return LineMatching.MakeDiffList(@this.Match(originalLines, modifiedLines), originalLines, modifiedLines);
    }

    public static IEnumerable<Patch> MakePatches(
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