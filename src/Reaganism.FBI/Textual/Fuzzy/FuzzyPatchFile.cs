using System.Collections.Generic;
using System.Text;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     A fuzzy patch file, containing each <see cref="FuzzyPatch"/> as well as
///     the original and modified file paths (if provided).
/// </summary>
/// <param name="Patches">The patches within the file.</param>
/// <param name="OriginalPath">The path to the original file.</param>
/// <param name="ModifiedPath">The path to the modified file.</param>
public readonly partial record struct FuzzyPatchFile(IEnumerable<FuzzyPatch> Patches, string? OriginalPath, string? ModifiedPath)
{
#region Serialization
    /// <summary>
    ///     Produces a string representation of the patch file.
    /// </summary>
    /// <param name="autoOffset">
    ///     Whether to automatically determine the insertion offset.
    /// </param>
    /// <param name="originalPath">
    ///     The path to the original file, if you want to override the value.
    /// </param>
    /// <param name="modifiedPath">
    ///     The path to the modified file, if you want to override the value.
    /// </param>
    public string ToString(bool autoOffset, string? originalPath = null, string? modifiedPath = null)
    {
        originalPath ??= OriginalPath;
        modifiedPath ??= ModifiedPath;

        var sb = new StringBuilder();
        {
            if (originalPath is not null && modifiedPath is not null)
            {
                sb.Append("--- ").AppendLine(originalPath);
                sb.Append("+++ ").AppendLine(modifiedPath);
            }

            foreach (var patch in Patches)
            {
                sb.AppendLine(FuzzyPatchHeader.GetHeader(patch, autoOffset));

                foreach (var diff in patch.Diffs)
                {
                    diff.AppendLine(sb);
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Produces a string representation of the patch file using the
    ///     values of <see cref="OriginalPath"/> and <see cref="ModifiedPath"/>
    ///     and without automatically determining the insertion offset.
    /// </summary>
    public override string ToString()
    {
        return ToString(false);
    }
#endregion
}