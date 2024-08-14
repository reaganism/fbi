using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

namespace Reaganism.FBI;

partial class PatchFile
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
    [PublicAPI]
    public static PatchFile FromLines(IEnumerable<string> lines, bool verifyHeaders = true)
    {
        var patches = new List<ReadOnlyPatch>();
        var patch   = default(Patch);
        var delta   = 0;

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
            }

            switch (line[0])
            {
                case '@':
                    if (patch is not null)
                    {
                        // Entered new patch, complete old one.
                        patches.Add(patch.AsReadOnly());
                    }

                    var match = hunk_offset_regex.Match(line);
                    if (!match.Success)
                    {
                        throw new InvalidDataException($"Invalid hunk offset({i}): {line}");
                    }

                    patch = new Patch
                    {
                        Start1  = int.Parse(match.Groups[1].Value) - 1,
                        Length1 = int.Parse(match.Groups[2].Value),
                        Length2 = int.Parse(match.Groups[4].Value),
                    };

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
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new DiffLine(Operation.EQUALS, line[1..]));
                    break;

                case '+':
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new DiffLine(Operation.INSERT, line[1..]));
                    break;

                case '-':
                    Debug.Assert(patch is not null);
                    patch.Diffs.Add(new DiffLine(Operation.DELETE, line[1..]));
                    break;

                default:
                    throw new InvalidDataException($"Invalid line({i}): {line}");
            }
        }

        // Add the last patch.
        if (patch is not null)
        {
            patches.Add(patch.AsReadOnly());
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