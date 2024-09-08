using System.Collections.Generic;
using System.IO;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities.Extensions;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     Static diffing operations.
/// </summary>
[PublicAPI]
public static class Differ
{
    [PublicAPI]
    public const int DEFAULT_CONTEXT_COUNT = 3;

    /// <summary>
    ///     Produces a patch file from two files (an original file and a
    ///     modified file).
    /// </summary>
    /// <param name="differ">The differ to use for diffing.</param>
    /// <param name="originalPath">The path to the original file.</param>
    /// <param name="modifiedPath">The path to the modified file.</param>
    /// <param name="rootDir">The root directory shared by each file.</param>
    /// <param name="contextLinesCount">
    ///     The amount of surrounding context.
    /// </param>
    /// <param name="collate">Whether patches should be collated.</param>
    /// <returns>The patch file containing all patches within a file.</returns>
    [PublicAPI]
    public static PatchFile DiffFiles(
        IDiffer differ,
        string  originalPath,
        string  modifiedPath,
        string? rootDir           = null,
        int     contextLinesCount = DEFAULT_CONTEXT_COUNT,
        bool    collate           = true
    )
    {
        return new PatchFile(
            patches: differ.MakePatches(
                File.ReadAllLines(Path.Combine(rootDir ?? "", originalPath)),
                File.ReadAllLines(Path.Combine(rootDir ?? "", modifiedPath)),
                contextLinesCount,
                collate
            ).ToList(),
            originalPath,
            modifiedPath
        );
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
    internal static IEnumerable<CompiledPatch> MakePatches(
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