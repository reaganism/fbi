using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Reaganism.FBI.Textual.Fuzzy;

partial record struct FuzzyPatchFile
{
    // TODO: See if we can optimize out RegEx usage?
    private static readonly Regex hunk_offset_regex = HunkOffsetRegex();

    /// <summary>
    ///     Creates a patch file from the given text.
    /// </summary>
    /// <param name="patchText">The text to parse.</param>
    /// <param name="verifyHeaders">
    ///     Whether header offsets should be matched and validated.
    /// </param>
    /// <returns>
    ///     A <see cref="FuzzyPatchFile"/> with the parsed patches.
    /// </returns>
    public static FuzzyPatchFile FromText(
        string patchText,
        bool   verifyHeaders = true
    )
    {
        return FromLines(patchText.Split('\n').Select(x => x.TrimEnd('\r').AsMemory()), verifyHeaders);
    }

    /// <summary>
    ///     Creates a patch file from the given lines.
    /// </summary>
    /// <param name="lines">The lines to parse.</param>
    /// <param name="verifyHeaders">
    ///     Whether header offsets should be matched and validated.
    /// </param>
    /// <returns>
    ///     A <see cref="FuzzyPatchFile"/> with the parsed patches.
    /// </returns>
    public static FuzzyPatchFile FromLines(
        IEnumerable<string> lines,
        bool                verifyHeaders = true
    )
    {
        return FromLines(lines.Select(x => x.AsMemory()), verifyHeaders);
    }

    /// <summary>
    ///     Creates a patch file from the given lines.
    /// </summary>
    /// <param name="lines">The lines to parse.</param>
    /// <param name="verifyHeaders">
    ///     Whether header offsets should be matched and validated.
    /// </param>
    /// <returns>
    ///     A <see cref="FuzzyPatchFile"/> with the parsed patches.
    /// </returns>
    public static FuzzyPatchFile FromLines(
        IEnumerable<ReadOnlyMemory<char>> lines,
        bool                              verifyHeaders = true
    )
    {
        var patches = new List<FuzzyPatch>();
        var patch   = default(FuzzyPatch);
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

            var span = line.Span;

            // Parse context lines.
            {
                // If the patch hasn't been created, context is still allowed.
                // If we haven't encountered a patch header ('@@ ...'), handle
                // the context.
                if (patch is null && span[0] != '@')
                {
                    if (i == 1 && span.StartsWith("--- "))
                    {
                        originalPath = line[4..].ToString();
                    }
                    else if (i == 2 && span.StartsWith("+++ "))
                    {
                        modifiedPath = line[4..].ToString();
                    }
                    else
                    {
                        throw new InvalidDataException($"Invalid context line({i}): {line}");
                    }

                    continue;
                }
            }

            // Parse actual patch bodies (headers and contents).
            switch (span[0])
            {
                // Patch header.
                case '@':
                    // TODO(perf): Necessary string allocation for regex? Sucks.
                    var match = hunk_offset_regex.Match(span.ToString());
                    if (!match.Success)
                    {
                        throw new InvalidDataException($"Invalid hunk offset({i}): {line}");
                    }

                    patch = new FuzzyPatch
                    {
                        Start1  = int.Parse(match.Groups[1].Value) - 1,
                        Length1 = int.Parse(match.Groups[2].Value),
                        Start2  = int.Parse(match.Groups[3].Value) - 1,
                    };

                    // Range2 start may be automatically determined.
                    if (match.Groups[3].Value == "_")
                    {
                        patch.Start2 = patch.Start1 + delta;
                    }
                    else
                    {
                        patch.Start2 = int.Parse(match.Groups[3].Value) - 1;

                        if (verifyHeaders && patch.Start2 != patch.Start1 + delta)
                        {
                            throw new InvalidDataException($"APplied offset mismatch; expected: {patch.Start1 + delta + 1}, actual: {patch.Start2 + 1}");
                        }
                    }

                    delta += patch.Length2 - patch.Length1;
                    patches.Add(patch);
                    break;

                // EQUALS patch line.
                case ' ':
                    Debug.Assert(patch is not null, "Encountered patch contents before patch has been created");

                    patch.Diffs.Add(new FuzzyDiffLine(FuzzyOperation.EQUALS, line, true));
                    break;

                // INSERT patch line.
                case '+':
                    Debug.Assert(patch is not null, "Encountered patch contents before patch has been created");

                    patch.Diffs.Add(new FuzzyDiffLine(FuzzyOperation.INSERT, line, true));
                    break;

                // DELETE patch line.
                case '-':
                    Debug.Assert(patch is not null, "Encountered patch contents before patch has been created");

                    patch.Diffs.Add(new FuzzyDiffLine(FuzzyOperation.DELETE, line, true));
                    break;

                default:
                    throw new InvalidDataException($"Invalid line({i}): {line}");
            }
        }

        if (verifyHeaders)
        {
            foreach (var patchToVerify in patches)
            {
                var header = FuzzyPatchHeader.GetHeader(patchToVerify, false);

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

        return new FuzzyPatchFile(patches, originalPath, modifiedPath);
    }

    [GeneratedRegex(@"@@ -(\d+),(\d+) \+([_\d]+),(\d+) @@")]
    private static partial Regex HunkOffsetRegex();
}