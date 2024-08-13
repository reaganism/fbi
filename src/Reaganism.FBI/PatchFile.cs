using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Reaganism.FBI;

/// <summary>
///     A patch file, containing a collection of <see cref="ReadOnlyPatch"/>es.
/// </summary>
public partial class PatchFile(List<ReadOnlyPatch> patches, string originalPatch, string modifiedPath)
{
    private static readonly Regex hunk_offset_regex = HunkOffsetRegex();

    public List<ReadOnlyPatch> Patches { get; } = patches;

    public string OriginalPath { get; set; } = originalPatch;

    public string ModifiedPath { get; set; } = modifiedPath;

    /// <summary>
    ///     Creates a patch file from the given text.
    /// </summary>
    /// <param name="patchText">The text to parse.</param>
    /// <param name="verifyHeaders">
    ///     Whether header offsets should be verified.
    /// </param>
    /// <returns>
    ///     A <see cref="PatchFile"/> instance containing the parsed patches.
    /// </returns>
    public static PatchFile FromText(string patchText, bool verifyHeaders = true)
    {
        // TODO: Can we trim all whitespace and not just carriage returns?
        return FromLines(patchText.Split('\n').Select(x => x.TrimEnd('\r')), verifyHeaders);
    }

    /// <summary>
    ///     Creates a patch file from the given lines.
    /// </summary>
    /// <param name="lines">The lines to parse.</param>
    /// <param name="verifyHeaders">
    ///     Whether header offsets should be verified.
    /// </param>
    /// <returns>
    ///     A <see cref="PatchFile"/> instance containing the parsed patches.
    /// </returns>
    public static PatchFile FromLines(IEnumerable<string> lines, bool verifyHeaders = true)
    {
        var patches = new List<ReadOnlyPatch>();
        var patch   = default(Patch);
        var delta   = 0;

        var originalPath = string.Empty;
        var modifiedPath = string.Empty;

        var i = 0;
        foreach (var line in lines)
        {
            i++;

            // Ignore empty lines.
            if (line.Length == 0)
            {
                continue;
            }

            // Parse context lines.
            {
                if (patch is null && line[0] != '@')
                {
                    if (i == 1 && line.StartsWith("--- "))
                    {
                        originalPath = line[4..];
                    }
                    else if (i == 2 && line.StartsWith("+++ "))
                    {
                        modifiedPath = line[4..];
                    }
                    else
                    {
                        throw new InvalidDataException($"Invalid context line({i}): {line}");
                    }

                    continue;
                }

                Debug.Assert(!string.IsNullOrEmpty(originalPath));
                Debug.Assert(!string.IsNullOrEmpty(modifiedPath));
            }

            switch (line[0])
            {
                case '@':
                    var match = hunk_offset_regex.Match(line);
                    if (!match.Success)
                    {
                        throw new InvalidDataException($"Invalid hunk offset({i}): {line}");
                    }

                    patch = new Patch
                    {
                        Range1 = new LineRange(int.Parse(match.Groups[1].Value) - 1, int.Parse(match.Groups[2].Value)),
                        Range2 = new LineRange(0,                                    int.Parse(match.Groups[4].Value)),
                    };

                    // Range2 start is automatically determined.
                    if (match.Groups[3].Value == "_")
                    {
                        patch.Range2 = patch.Range2 with { Start = patch.Range1.Start + delta };
                    }
                    else
                    {
                        patch.Range2 = patch.Range2 with { Start = int.Parse(match.Groups[3].Value) - 1 };

                        if (verifyHeaders && patch.Range2.Start != patch.Range1.Start + delta)
                        {
                            throw new InvalidDataException($"Applied offset mismatch; expected: {patch.Range1.Start + delta + 1}, actual: {patch.Range2.Start + 1}");
                        }
                    }

                    delta += patch.Range2.Length - patch.Range1.Length;
                    patches.Add(new ReadOnlyPatch(patch));
                    break;

                case ' ':
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new Diff(Operation.EQUALS, line[1..]));
                    break;

                case '+':
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new Diff(Operation.INSERT, line[1..]));
                    break;

                case '-':
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new Diff(Operation.DELETE, line[1..]));
                    break;

                default:
                    throw new InvalidDataException($"Invalid line({i}): {line}");
            }
        }

        if (verifyHeaders)
        {
            foreach (var patchToVerify in patches)
            {
                var header = Patch.GetHeader(patchToVerify.Range1, patchToVerify.Range2, false);

                if (patchToVerify.Range1.Length != patchToVerify.ContextLines.Count)
                {
                    throw new InvalidDataException($"Context length does not match contents: {header}");
                }

                if (patchToVerify.Range2.Length != patchToVerify.PatchedLines.Count)
                {
                    throw new InvalidDataException($"Patched length does not match contents: {header}");
                }
            }
        }

        return new PatchFile(patches, originalPath, modifiedPath);
    }

    [GeneratedRegex(@"@@ -(\d+),(\d+) \+([_\d]+),(\d+) @@")]
    private static partial Regex HunkOffsetRegex();
}