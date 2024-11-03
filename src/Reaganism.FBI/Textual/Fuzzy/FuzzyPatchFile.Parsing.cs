using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities;

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
    [PublicAPI]
    public static FuzzyPatchFile FromText(
        string patchText,
        bool   verifyHeaders = true
    )
    {
        return FromLines(patchText.Split('\n').Select(x => Utf16String.FromSpan(x.TrimEnd('\r').AsSpan())), verifyHeaders);
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
    [PublicAPI]
    public static FuzzyPatchFile FromLines(
        IEnumerable<string> lines,
        bool                verifyHeaders = true
    )
    {
        return FromLines(lines.Select(x => Utf16String.FromSpan(x.AsSpan())), verifyHeaders);
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
    [PublicAPI]
    public static FuzzyPatchFile FromLines(
        IEnumerable<Utf16String> lines,
        bool                     verifyHeaders = true
    )
    {
        var patches = new List<FuzzyPatch>();
        var patch   = default(FuzzyPatch);
        var delta   = 0;

        var originalPath = default(Utf16String?);
        var modifiedPath = default(Utf16String?);

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
                    switch (i)
                    {
                        case 1 when span.StartsWith("--- "):
                            originalPath = line[4..];
                            break;

                        case 2 when span.StartsWith("+++ "):
                            modifiedPath = line[4..];
                            break;

                        default:
                            throw new InvalidDataException($"Invalid context line({i}): {span}");
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
                        throw new InvalidDataException($"Invalid hunk offset({i}): {span}");
                    }

                    patch = new FuzzyPatch
                    {
                        Start1  = int.Parse(match.Groups[1].Value) - 1,
                        Length1 = int.Parse(match.Groups[2].Value),
                        Length2 = int.Parse(match.Groups[4].Value),
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
                            throw new InvalidDataException($"Applied offset mismatch; expected: {patch.Start1 + delta + 1}, actual: {patch.Start2 + 1}");
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
                    throw new InvalidDataException($"Invalid line({i}): {span}");
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