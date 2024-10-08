using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

namespace Reaganism.FBI;

partial struct PatchFile
{
    private static readonly Regex hunk_offset_regex = HunkOffsetRegex();

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
    [PublicAPI]
    public static PatchFile FromText(string patchText, bool verifyHeaders = true)
    {
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
    [PublicAPI]
    public static PatchFile FromLines(IEnumerable<string> lines, bool verifyHeaders = true)
    {
        var patches      = new List<CompiledPatch>();
        var patch        = default(Patch);
        var patchCreated = false;
        var delta        = 0;

        var originalPath = default(string);
        var modifiedPath = default(string);

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
                if (!patchCreated && line[0] != '@')
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
            }

            switch (line[0])
            {
                case '@':
                    if (patchCreated)
                    {
                        // Entered new patch, complete old one.
                        patches.Add(patch.Compile());
                    }

                    var match = hunk_offset_regex.Match(line);
                    if (!match.Success)
                    {
                        throw new InvalidDataException($"Invalid hunk offset({i}): {line}");
                    }

                    patchCreated  = true;
                    patch         = new Patch();
                    patch.Start1  = int.Parse(match.Groups[1].Value) - 1;
                    patch.Length1 = int.Parse(match.Groups[2].Value);
                    patch.Length2 = int.Parse(match.Groups[4].Value);

                    // Range2 start is automatically determined.
                    if (match.Groups[3].Value == "_")
                    {
                        patch.Start2 = patch.Start1 + delta;
                    }
                    else
                    {
                        patch.Start2 = int.Parse(match.Groups[3].Value) - 1;

                        if (verifyHeaders && patch.Start2 != patch.Start1 + delta)
                        {
                            throw new InvalidDataException($"Applied offset mismatch; expected: {patch.Start1 + delta + 1}, actual: {patch.Start2 + 1}");
                        }
                    }

                    delta += patch.Length2 - patch.Length1;
                    // patches.Add(new ReadOnlyPatch(patch));
                    break;

                case ' ':
                    Debug.Assert(patchCreated);
                    patch.Diffs.Add(new DiffLine(Operation.EQUALS, line, true));
                    break;

                case '+':
                    Debug.Assert(patchCreated);
                    patch.Diffs.Add(new DiffLine(Operation.INSERT, line, true));
                    break;

                case '-':
                    Debug.Assert(patchCreated);
                    patch.Diffs.Add(new DiffLine(Operation.DELETE, line, true));
                    break;

                default:
                    throw new InvalidDataException($"Invalid line({i}): {line}");
            }
        }

        // Add the last patch.
        if (patchCreated)
        {
            patches.Add(patch.Compile());
        }

        if (verifyHeaders)
        {
            foreach (var patchToVerify in patches)
            {
                var header = Patch.GetHeader(patchToVerify, false);

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