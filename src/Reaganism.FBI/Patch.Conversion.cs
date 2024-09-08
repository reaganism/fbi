using JetBrains.Annotations;

namespace Reaganism.FBI;

partial class Patch
{
    /// <summary>
    ///     Creates a <see cref="CompiledPatch"/> derived from the current state
    ///     of this patch.
    /// </summary>
    /// <returns>The read-only patch with extra information.</returns>
    [PublicAPI]
    public CompiledPatch AsReadOnly()
    {
        return new CompiledPatch(this);
    }

    /// <summary>
    ///     Clones this patch.
    /// </summary>
    /// <returns>
    ///     A new <see cref="Patch"/> instance with the same data but without
    ///     any references to the original.
    /// </returns>
    [PublicAPI]
    public Patch Clone()
    {
        return new Patch(this);
    }
}