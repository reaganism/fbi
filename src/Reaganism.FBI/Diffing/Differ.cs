using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Reaganism.FBI.Diffing;

public static class Differ
{
    public const int DEFAULT_CONTEXT_COUNT = 3;

    public static PatchFile DiffFiles(
        this IDiffer @this,
        string       originalPath,
        string       modifiedPath,
        string?      rootDir           = null,
        int          contextLinesCount = DEFAULT_CONTEXT_COUNT,
        bool         collate           = true
    )
    {
        return new PatchFile(
            patches: @this.MakePatches(
                File.ReadAllLines(Path.Combine(rootDir ?? "", originalPath)),
                File.ReadAllLines(Path.Combine(rootDir ?? "", modifiedPath)),
                contextLinesCount,
                collate
            ).ToList(),
            originalPath,
            modifiedPath
        );
    }

    internal static IEnumerable<ReadOnlyPatch> MakePatches(
        List<DiffLine> diffs,
        int            contextLinesCount = DEFAULT_CONTEXT_COUNT,
        bool           collate           = true
    )
    {
        var patch = new Patch(diffs)
                   .RecalculateLength()
                   .Trim(contextLinesCount);

        if (patch.Length1 == 0)
        {
            return [];
        }

        if (!collate)
        {
            patch.Uncollate();
        }

        return patch.Split(contextLinesCount).Select(x => x.AsReadOnly());
    }
}