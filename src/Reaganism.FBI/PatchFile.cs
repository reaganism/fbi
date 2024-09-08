using System.Collections.Generic;
using System.Text;

using JetBrains.Annotations;

namespace Reaganism.FBI;

/// <summary>
///     A patch file, containing a collection of <see cref="CompiledPatch"/>es.
/// </summary>
[PublicAPI]
public readonly partial struct PatchFile(List<CompiledPatch> patches, string? originalPatch, string? modifiedPath)
{
    /// <summary>
    ///     The patches contained in this patch file.
    /// </summary>
    [PublicAPI]
    public List<CompiledPatch> Patches { [PublicAPI] get; } = patches;

    /// <summary>
    ///     The original path of the file being patched.
    /// </summary>
    [PublicAPI]
    public string? OriginalPath { [PublicAPI] get; } = originalPatch;

    /// <summary>
    ///     The modified path of the file being patched.
    /// </summary>
    [PublicAPI]
    public string? ModifiedPath { [PublicAPI] get; } = modifiedPath;

#region Serialization
    [PublicAPI]
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
                sb.AppendLine(Patch.GetHeader(patch, autoOffset));
                {
                    foreach (var diff in patch.Diffs)
                    {
                        diff.AppendLine(sb);
                    }
                }
            }
        }

        return sb.ToString();
    }

    [PublicAPI]
    public override string ToString()
    {
        return ToString(false);
    }
#endregion
}